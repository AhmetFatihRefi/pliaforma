# Sprint 45 — Simplify Payments to Single Due Date

> **Durum:** Onaylandı — implementasyon. Ön koşul: working tree temiz, Sprint 44 tag'li
> (`v1.35.0-pilot` → `eb24ad4ad464286237eee73d696974c0fcf52f43`, "fix: preserve input focus in modal forms").
> Sprint 45 ayrı scope; Sprint 44 ile karıştırılmaz.

## Ürün kararı

Taksitli ödeme planı **kullanıcıdan tamamen kaldırılır.** Bir sözleşme satırının (line) tek gerçeği:

- `TotalPrice` (toplam tutar) + `DueDate` (**Son ödeme tarihi**) + line'a bağlı **alınan** ödemeler (Payment, Paid).
- Kullanıcı vadeye kadar istediği kadar parça parça öder.
- `Kalan ödeme = max(TotalPrice − Ödenen, 0)`; vade geçince ödenmeyen kısım **gecikmiş borç**.
- Ödeme yapıldıkça vade **değişmez**; ödeme tarihi vadeyi ileri/geri taşımaz.

Örnek: Toplam 10.000 ₺, Son ödeme 30.07.2026.
- 30.07 öncesi 2.000 ödenir → Ödenen 2.000, Kalan 8.000, Gecikmiş 0.
- 30.07 sonrası hâlâ 8.000 ödenmemiş → Kalan 8.000, Gecikmiş 8.000, Gecikme başlangıcı 30.07.2026.
- Sonra 3.000 daha → Ödenen 5.000, Kalan 5.000, Gecikmiş 5.000, Gecikme başlangıcı yine 30.07.2026.

## Kesin kararlar

1. **Kaldırma stratejisi = Alt A (Soft removal).** `ContractLinePaymentInstallment` entity/tablo/DbSet/migration
   **kalır** (`[Obsolete]` + TECH-DEBT girişi). UI, yeni-sözleşme akışı ve payment-plan endpoint'leri devre dışı.
   **DROP TABLE / destructive migration YOK.**
2. **DueDate.** Yeni sözleşme satırlarında Son ödeme tarihi **zorunlu** (validator + UI). DB nullable kalır (legacy).
   Legacy null DueDate → "Son ödeme tarihi belirlenmemiş"; **null hiçbir zaman gecikmiş sayılmaz.**
3. **Payment lifecycle.** Yeni Payment = **alınan para**. Yeni kayıt doğrudan `Status=Paid`, `PaidAt=now`.
   UI'da Pending ödeme / vade / "taksit ödendi işaretle" mantığı kalkar. `MarkPaymentPaid` endpoint'i legacy için kalır
   ama yeni UI göstermez. Payment.DueDate tabanlı ikinci gecikme kavramı UI'da kalmaz.
4. **Legacy backfill.** 2+ aktif installment'ı olan line'larda `line.DueDate = MAX(active installment.DueDate)`
   (non-destructive; installment kayıtları silinmez). **Önce dry-run raporu** (kaç line, örnek eski→yeni);
   rapor onaylanmadan gerçek UPDATE yapılmaz.

## Borç / gecikme hesap kuralları (line seviyesi — TEK kaynak)

```
paidAmount      = Σ Payment(Status=Paid, lineId=L)  (+ tek-line-header legacy fallback)
remainingAmount = max(TotalPrice - paidAmount, 0)
overpaidAmount  = max(paidAmount - TotalPrice, 0)
isOverdue       = dueDate != null && today > dueDate && remainingAmount > 0
overdueAmount   = isOverdue ? remainingAmount : 0
daysOverdue     = isOverdue ? (today - dueDate) : 0
today           = stüdyo-yerel gün (SessionRules.StudioNowAsync)
```

- `today == dueDate` → gecikmiş **DEĞİL** (strict `>`).
- `dueDate == null` → hiç gecikmiş değil.
- Kısmi ödeme `remaining`/`overdue`'yu azaltır; `dueDate` sabit. Tam ödeme → borç 0.
- `remaining` **0'a floor'lanır**; overpay `overpaidAmount` ile ayrı gösterilir.

## A. Backend

### A1. ContractLine DTO (`ContractLineFeature.cs`)
- `StudentContractLineDto`'ya ekle: `remainingAmount` (floor), `overpaidAmount`, `isOverdue`, `overdueAmount`, `daysOverdue`.
- Hesap `BuildAsync` içinde tek yerde (tüm ekranlar buradan beslenir). `today` = studio-local.
- `ComputeDebtAsync` currency rollup floor'lu remaining ile (Sprint 41 grouping korunur).

### A2. Payment create (`PaymentFeature.cs`)
- `CreatePaymentCommand` handler → `Status = Paid`, `PaidAt = clock.UtcNow`, `DueDate = today` (dahili; UI'dan alınmaz).
- Payment installment'a bağlanmaz; borç motoru yalnız Paid ödemeleri sayar (mevcut davranış).
- `MarkPaymentPaidCommand` endpoint'i legacy için kalır.

### A3. Payment plan soft removal
- `CreateStudentContractLineCommand.PaymentPlan` parametresi + validasyonu kaldırılır (kullanılmaz).
- `GET/PUT /api/studio/contract-lines/{lineId}/payment-plan` → **410 Gone**, mesaj:
  "Taksitli ödeme planı kaldırıldı. Lütfen Son ödeme tarihi ve alınan ödemeleri kullanın."
- `ContractLinePaymentPlan.cs` + `ContractLinePaymentInstallment` entity `[Obsolete]`; TECH-DEBT girişi.

### A4. DueDate zorunlu
- `CreateStudentContractLineValidator` → `DueDate` NotEmpty. DB nullable kalır.

### A5. Dashboard / reports (line-bazlı gecikme)
- `StudioDashboard.cs`: `paid = Σ Payment(Paid, dönem)`; `pending/overdue` **line'dan** (`Σ remaining`, `Σ overdue`, aktif line).
- `StudentDashboard.cs` `paymentSummary`: `pendingAmount → Σ remaining`, `overdueAmount → Σ overdue`,
  `nextDueDate → en yakın gelecek line.DueDate`.
- `PaymentsReport.cs`: gecikme line-borç kaynağı; ödeme tablosu "alınan ödemeler". Kart/tablo düzeni korunur, kaynak sadeleşir.
- Payment.Status/Payment.DueDate tabanlı "gecikmiş ödeme" UI'dan kalkar. Çoklu para birimi grouping korunur.

### A6. Reminder (`ReminderScanner.cs`)
- Line reminder `line.DueDate` kullanıyor → **değişmez.**
- Yeni Payment=Paid modelinde yeni kayıtlar Pending üretmez → Payment due-soon/overdue reminder yeni kayıt üretmez.
- Legacy pending payment davranışı bozulmaz; yeni UX bekleyen payment üretmez.

## B. Frontend

### B1. Yeni sözleşme
- "Taksitli/Peşin", "Ödeme planı", "Taksitler", "1./2. taksit" tamamen kalkar.
- Sadece **zorunlu** "Son ödeme tarihi" alanı. Boşsa UI validasyon.

### B2. Contract detail / student finance
- Payment plan section kalkar.
- Gösterilen: Kalan ödeme / Gecikmiş borç / Son ödeme tarihi / Ödenen tutar / Toplam tutar.
- DueDate null → "Son ödeme tarihi belirlenmemiş".

### B3. Payments
- Ödeme ekleme = **alınan ödeme** kaydı; **vade alanı kalkar**; "Ödendi işaretle" yeni UI'dan kalkar.
- "Taksit" kelimesi kalkar. Kalan/gecikmiş line DTO'dan. Çoklu para birimi bozulmaz.

### B4. Dil temizliği
Kaldır: Taksitli ödeme · Taksit planı · Taksit · Ödeme planı · Planned/Paid installment.
Kullan: Son ödeme tarihi · Kalan ödeme · Gecikmiş borç · Ödenen tutar · Toplam tutar · Vadesi geçti · Vadesi bugün · Vadesi gelmedi.

## C. Data / migration
- **Schema migration YOK** (tercih). Entity/table/DbSet silme yok. DROP TABLE yok.
- Legacy backfill (karar #4): önce **dry-run raporu** (etkilenen line sayısı + örnek eski→yeni DueDate).
  Rapor olmadan veriye UPDATE yok. Uygulama biçimi (data-only migration vs idempotent script) uygulamadan önce net yazılır.
  Rollback: installment kayıtları durduğu için `line.DueDate = MIN(active installment.DueDate)` ile geri hesaplanır.

## D. Kapsam dışı
Online ödeme · banka entegrasyonu · otomatik tahsilat · fatura/e-arşiv · komisyon · taksiti başka isimle geri getirme ·
installment tablosunu DROP etme · büyük rapor redesign · payment import · SMS/WhatsApp ödeme hatırlatma.

## E. Validation beklentisi

**Backend:** `dotnet build` 0 · `migrations has-pending` clean · DueDate boş→400, dolu→success · yeni payment doğrudan Paid + PaidAt set ·
kısmi ödeme remaining azaltır · `today<dueDate`→overdue 0 · `today==dueDate`→overdue 0 · `today>dueDate && remaining>0`→overdueAmount=remaining ·
tam ödeme→overdue 0 · overpay→remaining 0, overpaidAmount>0 · dueDate null→overdue false · payment-plan endpoint 410 ·
reminder line.DueDate ile çalışır · çoklu para birimi grouping bozulmadı.

**Frontend:** `tsc --noEmit` 0 · `next build` 0 · yeni sözleşmede taksit/ödeme planı yok · Son ödeme tarihi zorunlu ·
ödeme ekle modalında vade yok · "Ödendi işaretle" yeni UI'da yok · "Taksit" kelimesi UI'da yok ·
öğrenci detay/contract detail/payments/dashboard/reports line-bazlı kalan/gecikmiş · legacy DueDate null düzgün · çoklu para birimi bozulmadı.

**Smoke:** `scripts/sprint38-smoke.sh` kaldır → `scripts/sprint45-smoke.sh`:
line due date + partial payment + overdue + overpay + legacy null due date + payment=Paid akışı.
