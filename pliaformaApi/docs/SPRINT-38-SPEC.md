# SPRINT 38 — SPEC & ANALİZ: Ödeme Planı / Taksit Takibi / Vade Tarihleri

> **Durum:** SPEC turu — onay bekliyor. Kod YOK, migration YOK, commit/push/tag YOK.
> **Hedef:** Sözleşme satırı (line) için **peşin** veya **taksitli** ödeme planı tanımlanabilsin; vade/tutar/gecikme takip edilebilsin. Mevcut line-bazlı ödeme/borç matematiği ve legacy ödemeler **bozulmadan**.

---

## 0. Özet karar tablosu (öneriler — onay bekliyor)

| # | Konu | Öneri |
|---|---|---|
| D1 | Model | **Ayrı tablo** `ContractLinePaymentInstallment` (line'a bağlı). Yalnız plan/vade/tutar tutar. |
| D2 | Status | **Hesaplanan** (DB'de tutulmaz). Stored: `SequenceNo, DueDate, Amount, Notes?, IsActive`. Planned/PartiallyPaid/Paid/Overdue türetilir. |
| D3 | Payment ↔ installment | **MVP-A: Payment'a InstallmentId FK YOK.** Ödeme yine `line`'a bağlı kalır; taksit durumu, line'ın toplam ödemesi **en eski vadeden sırayla** dağıtılarak hesaplanır. Allocation tablosu YOK. |
| D4 | PaymentPlanType kolonu | **GEREKSİZ** (çift kaynak olur). 1 taksit = peşin, 2+ = taksitli; "plan yok" = installment satırı yok. UI'daki Peşin/Taksitli yalnız planı kurmak için. |
| D5 | Currency | Installment tablosunda **YOK** — `line.Currency`'den türetilir (tek kaynak, mismatch yok). |
| D6 | Taksit üretimi | Otomatik aylık + yuvarlama + kısa-ay **frontend'de** hesaplanır; backend **açık installment listesi** alır + `Σamount == line.TotalPrice` doğrular (preferredSlots deseni). |
| D7 | Plan zorunlu mu? | **Opsiyonel (MVP)** — legacy create/smoke bozulmasın. UI güçlü öneri. (Pilotta zorunlu → ayrı karar; eski akışı kırar.) |
| D8 | Plan güncelleme | **Her zaman full-replace serbest** (ödeme olsa bile) — allocation hesaplanan olduğu için veri bozulmaz; ödeme varsa UI **uyarır**. `Σ == TotalPrice` şart. |
| D9 | Reminder entegrasyonu | Plan kaydında `line.DueDate = en erken (ödenmemiş) taksit vadesi` senkronlanır → Sprint 28D reminder **kod değişmeden** çalışır. |
| D10 | Yetki | Yazma + okuma `Owner/StudioAdmin` (ödeme finansaldır; instructor görmez). Öğrenci read-view = faz-2. |
| D11 | Migration | Tek yeni tablo `Sprint38_ContractLinePaymentInstallments`, additive, **backfill YOK**. |

---

## 1. Mevcut sistem analizi (kod-temelli — doğrulandı)

### 1.1 Payment entity & borç
- `Payment` (`Domain/Entities/Payment.cs`): `StudioId, StudentProfileId, StudentPackageId?, StudentContractId?, StudentContractLineId?, Amount (numeric(12,2)), Currency, DueDate (DateOnly), PaidAt? (UTC), Status, Method?, Notes`. Config: `Amount numeric(12,2)`, index `(StudioId, DueDate)`, `(StudioId, StudentContractLineId)`, FK'ler SetNull.
- **Sprint 29A strict:** `CreatePaymentValidator` → `StudentContractLineId` **NotEmpty** (line zorunlu). Currency line ile eşleşmeli. Amount ≥ 0.
- `PaymentStatus { Pending, Paid, Overdue, Cancelled, Refunded }` — **Overdue DİNAMİK**: `PaymentMapping.Effective(stored, paidAt, dueDate, today) = (Pending && paidAt==null && dueDate < today) ? Overdue : stored`. Kalıcı tutulmaz.
- Komutlar: Create / Update (Amount/DueDate/Method/Notes; line **immutable**) / MarkPaid (Status=Paid, PaidAt=utcNow) / Cancel. Endpoint'ler `PaymentsController` `[Authorize(Owner,StudioAdmin)]`.

### 1.2 Line borç matematiği (kalp)
`ContractLineFeature.cs` `ContractLineQuery.BuildAsync`:
```
DirectPaid(L)   = Σ Payment(Paid, lineId=L).Amount
HeaderLineCount = header(L) altındaki line sayısı
LegacyCandidate = Σ Payment(Paid, lineId=NULL, contractId=L.header).Amount
paid(L)         = DirectPaid + (HeaderLineCount == 1 ? LegacyCandidate : 0)   // tek-line header legacy fallback
Remaining(L)    = TotalPrice − paid(L)
```
`StudentContractLineDto` alanları: `... TotalPrice, Paid, Remaining, DueDate?, Currency, ...`. `ComputeDebtAsync` → `LineDebtSummaryDto(ByCurrency[], Lines[], UnassignedByCurrency[])` (cross-currency toplam yok).

### 1.3 line.DueDate (mevcut tek vade)
- `StudentContractLine.DueDate (DateOnly?)` — opsiyonel, tek vade. `CreateStudentContractLineCommand` `DueDate` alır; `UpdateContractLineCommand(LineId, DueDate?, Notes?)` yalnız bunu + Notes günceller. Index `(StudioId, DueDate)`.
- **Sprint 28D reminder** (`ReminderScanner.cs`): `Active && DueDate != null && DueDate <= studioToday && Remaining > 0` olan line'lar → öğrenci-bazlı haftalık `LinePaymentDue` bildirimi (ISO-week dedup `line-payment-due:{studentId}:{week}`). `ReminderBackgroundService` 300sn (dev'de kapalı). Studio-local "today" = `SessionRules.StudioNowAsync` (`Studio.TimeZoneId`, fallback Türkiye).

### 1.4 Para/yuvarlama
- Tüm tutar alanları `numeric(12,2)` / `HasPrecision(12,2)`. `totalPrice = decimal.Round(LessonCount * PricePerLesson, 2)`.

### 1.5 Frontend
- Sözleşme oluştur: `students/[id]/contracts/new/page.tsx` — Ücret bölümü (ders başı ücret, currency, **computedTotal** read-only, **Son ödeme tarihi** opsiyonel). `ContractLineInput.dueDate`.
- `ContractDetailModal` (`students/_components.tsx`): "Ücret" Stat'ları (Toplam/Ödenen/Kalan/Telafi) + "Son ödeme tarihi" düzenleme (`updateLine`).
- `StudentFinanceModal`: ödeme kaydı (amount, dueDate, **line seçimi zorunlu**). `paymentService.create/update/markPaid/cancel`.
- `payments/page.tsx`: statusTone, **Overdue rozeti** (`Geciken N · tutar`), vade gösterimi. `money(n,cur) = n.toLocaleString("tr-TR")`.

### 1.6 Smoke yüzeyi
Ödeme/borç dokunan smoke'lar: **28a** (backfill paid/remaining), **28b** (line payment + debt + currency), **28d** (LinePaymentDue reminder), **23** (amendment debt), **24/25** (öğrenci debt görünürlüğü), **29a** (strict payment). `addline` helper'ı `dueDate` opsiyonel; `pay` helper'ı `{amount, dueDate, studentContractLineId}`.

---

## 2. Sorulara net cevaplar

| Soru | Cevap |
|---|---|
| **Mevcut DueDate tek vade için yeterli mi?** | Evet — peşin/legacy için `line.DueDate` yeter. Taksit (çoklu vade) için yetmez → yeni tablo. |
| **Taksit için yeni tablo gerekir mi?** | Evet, `ContractLinePaymentInstallment` (line'a bağlı, çok satır). |
| **Payment installment'a nasıl bağlanmalı?** | **Bağlanmasın (MVP-A).** Payment line'a bağlı kalır; durum **hesaplanır** (line ödemesi en eski vadeden dağıtılır). Bkz. §4. |
| **Legacy bozulmadan?** | Allocation = mevcut `line.Paid` değeri (legacy fallback dahil) üzerinden hesaplanır → eski ödemeler **otomatik** plana akar; Payment/borç şeması değişmez. |

---

## 3. Model — `ContractLinePaymentInstallment`

`BaseEntity, ITenantScoped`:
```
Id                       Guid
StudioId                 Guid  (FK Studio, Cascade)
StudentContractLineId    Guid  (FK StudentContractLine, Cascade)
SequenceNo               int   (1..N; DueDate asc sırasına göre atanır)
DueDate                  DateOnly
Amount                   decimal  (numeric(12,2); > 0)
Notes                    string?  (maxlen 500)
IsActive                 bool = true
(+ BaseEntity: CreatedAt/UpdatedAt/IsDeleted; + CreatedByUserId)
```
**Status YOK** (hesaplanan, D2). **Currency YOK** (line'dan, D5).

**EF config (`ContractLinePaymentInstallmentConfiguration`):**
- `ToTable("ContractLinePaymentInstallments")`, `Amount HasPrecision(12,2)`, `Notes maxlen 500`.
- FK Studio Cascade + StudentContractLine Cascade.
- Index `(StudioId, StudentContractLineId)`.
- **Unique index YOK** — full-replace soft-delete deseni (PreferredSlot/WorkingHours gibi); tekillik app katmanında.

**Migration:** `Sprint38_ContractLinePaymentInstallments` — tek tablo + index, additive, **backfill yok**.

> **Neden ayrı tablo (Studio'ya/line'a kolon değil):** çoklu vade + tutar tek kolonla ifade edilemez; line zaten header'dan ayrı "gerçek paket". `StudentContractLinePreferredSlot` birebir precedent (line'a bağlı çok-satır, full-replace, soft-delete).

---

## 4. Payment ↔ installment ilişkisi — MVP-A (hesaplanan allocation)

İki alternatif değerlendirildi:

- **A) FK yok + hesaplanan dağıtım (ÖNERİLEN):** Payment yalnız `line`'a bağlı. Taksit durumu okuma anında hesaplanır: line'ın toplam ödemesi en eski vadeden sırayla taksitlere dağıtılır. **Avantaj:** Payment entity/borç matematiği **hiç değişmez**, tek yeni tablo, legacy-safe, plan düzenleme veri bozmaz (yalnız yeniden hesaplanır). **Dezavantaj:** "şu taksiti kapat" niyeti net bağlanmaz (ama dağıtım kuralı deterministik).
- **B) `Payment.InstallmentId` nullable FK (veya allocation ara tablosu):** Net "bu ödeme bu taksiti kapatır". **Dezavantaj:** Payment migration + UI + bir ödemenin birden çok taksiti kapatması için `PaymentInstallmentAllocation` ara tablosu gerekir; mevcut strict-payment akışı + 28B borç sorgusu etkilenir.

**Karar (D3): A.** Bir tek ödeme birden çok taksiti kapatabilir (6.000 TL → 3×2.000 taksit hepsi Paid) — allocation bunu doğal yapar, ara tabloya **MVP'de gerek yok**. B / allocation tablosu **ileride** "taksit-bazlı tahsilat" gerekirse eklenir (additive).

### 4.1 Allocation algoritması (hesaplanan)
Girdi: `linePaid = line.Paid` (mevcut `ContractLineQuery` değeri — DirectPaid + tek-line-header legacy fallback; borçla **birebir tutarlı**), `installments` (aktif, sıralı: `DueDate asc, SequenceNo asc`), `today` (studio-local).

```
pool = linePaid
foreach inst in installments (sırayla):
    alloc          = min(inst.Amount, pool)
    inst.PaidAmount = alloc
    pool           -= alloc
    inst.Status =
        alloc >= inst.Amount            -> Paid
        else if inst.DueDate < today    -> Overdue        // 0 veya kısmi, vadesi geçmiş
        else if alloc > 0               -> PartiallyPaid
        else                            -> Planned         // gelecek vade, ödeme yok
overpaidAmount = max(0, pool)   // tüm taksitler kapandıktan sonra artan = fazla ödeme/kredi
```
- `today == DueDate` → **Overdue DEĞİL** (`DueDate < today` strict; mevcut payment-overdue ile aynı).
- Kısmi + vadesi geçmiş taksit → **Overdue** (precedence: geçmiş vade kısmiyi yener).

### 4.2 Legacy/örnek doğrulama
Plan: 15 Nis 2.000 / 15 May 2.000 / 15 Haz 2.000. Ödeme: 10 Nis 3.000 (Paid, line'a bağlı). → pool=3.000: 15 Nis alloc 2.000 (Paid), 15 May alloc 1.000 (PartiallyPaid veya vadesi geçtiyse Overdue), 15 Haz alloc 0 (Planned/Overdue). Kullanıcı örneğiyle **birebir**. ✓

---

## 5. Plan DTO (GET — hesaplanmış durum)

```
ContractLinePaymentPlanDto:
  lineId, currency, isConfigured (installment var mı), planType (Cash=1 taksit | Installment=2+),
  totalPlannedAmount   (= Σ Amount = line.TotalPrice; tutarlı plan şart)
  totalPaidAmount      (= line.Paid)
  remainingAmount      (= max(0, TotalPrice − Paid))
  overdueAmount        (= Σ (Amount − PaidAmount) over installments where DueDate < today && PaidAmount < Amount)
  overpaidAmount       (= max(0, Paid − TotalPrice))
  nextDueDate?, nextDueAmount?   (en erken PaidAmount<Amount taksit; hepsi ödendiyse null)
  counts: paidCount, partialCount, overdueCount, upcomingCount
  installments: [ { id, sequenceNo, dueDate, amount, paidAmount, status, notes } ]   // sıralı
```
`isConfigured=false` → installment yok (legacy/peşin değil); UI "Ödeme planı yok + oluştur" gösterir; borç yine `line.DueDate` + payments ile çalışır.

---

## 6. Peşin vs Taksitli davranışı

- **Peşin:** UI'da tek ödeme tarihi → **1 installment** (`SequenceNo=1, Amount=line.TotalPrice, DueDate=seçilen`). `planType=Cash`.
- **Taksitli:** 2+ installment. İki giriş yöntemi (frontend üretir, §D6):
  - **A. Otomatik aylık:** taksit sayısı + ilk vade + "her ay aynı gün". Tutar = eşit bölünür, **kalan fark son taksite** (Σ == TotalPrice). Kısa-ay: aynı gün mümkünse aynı gün, değilse **ayın son günü** (31 Oca + aylık → 28/29 Şub); UI açıklama gösterir.
  - **B. Özel tarihler:** kullanıcı her satıra `DueDate + Amount` girer; canlı toplam kontrolü.
- **Plan ≠ Payment:** Plan = ne zaman/ne kadar **beklenen**; Payment = fiilen **ödenen**. Peşin planı ödeme kaydı **oluşturmaz** — kullanıcı tahsilatı ayrıca `StudentFinanceModal`'dan Payment olarak girer (mevcut akış). Bu ayrım korunur.

### 6.1 Yuvarlama
2 ondalık; eşit bölüm `floor(TotalPrice / n, 2)`, **son taksit = TotalPrice − Σ(önceki)** → toplam her zaman `line.TotalPrice`. Örn. 10.000/3 → 3.333,33 / 3.333,33 / 3.333,34.

---

## 7. Validation (PUT / create paymentPlan)

- `installments` boş değil; her `Amount > 0` (0/negatif → 400); `DueDate` geçerli.
- **`Σ Amount == line.TotalPrice`** (2 ondalık, tam eşit) değilse → **400** ("Taksit toplamı sözleşme tutarına eşit olmalı: {TotalPrice}").
- Geçmiş tarihli vade **serbest** (uyarı UI'da; eski sözleşme girişi). Duplicate DueDate **serbest**.
- `SequenceNo` backend'de `DueDate asc` sıralanıp 1..N atanır (girilen sıra değil — D6 önerisi).
- Currency yok (line'dan).

---

## 8. Create / Update davranışı

- **Create line:** `CreateStudentContractLineCommand`'a opsiyonel `IReadOnlyList<InstallmentInput>? PaymentPlan` eklenir. Verilirse `Σ == TotalPrice` doğrulanıp installment'lar yazılır + `line.DueDate = en erken taksit vadesi` (D9). Verilmezse plan yok (legacy) — **D7: opsiyonel**.
- **PUT `/payment-plan`:** full-replace (mevcut aktif installment'ları soft-delete + yeni set). `UpdateContractLinePaymentPlanCommand(LineId, IReadOnlyList<InstallmentInput>)`.
- **Ödeme varken güncelleme (D8):** **serbest** — allocation hesaplanan olduğundan veri bozulmaz; UI uyarır ("Bu satırda N ödeme var; plan değişikliği taksit dağılımını yeniden hesaplar"). `Σ == TotalPrice` korunur. "Yalnız gelecek ödenmemiş taksit düzenlenebilir" gibi kısıt **MVP dışı** (gereksiz karmaşıklık; allocation zaten güvenli).
- **line.DueDate senkronu (D9):** her create/PUT sonrası `line.DueDate = installments.Min(DueDate)` (veya en erken ödenmemiş — basitlik için plan ilk vadesi). Böylece Sprint 28D reminder **kod değişmeden** çalışır. Plan silinirse `line.DueDate` korunur/elle yönetilir.

---

## 9. Gecikme / overdue

- Studio-local `today` (`SessionRules.StudioNowAsync`). `DueDate < today` strict.
- Taksit `PaidAmount < Amount && DueDate < today` → Overdue. `overdueAmount = Σ kalan`.
- `nextDueDate/nextDueAmount` = en erken `PaidAmount<Amount` taksit.
- UI: "Geciken ödeme var", "Sonraki ödeme: 15 May — 2.000 TL", "3 taksitin 1'i ödendi". Öğrenci/sözleşme detayında.

---

## 10. API önerisi

`ContractsController` (`[Authorize(Owner,StudioAdmin)]`) altına:
```
GET /api/studio/contract-lines/{lineId}/payment-plan   → ContractLinePaymentPlanDto (hesaplanmış)
PUT /api/studio/contract-lines/{lineId}/payment-plan   → full-replace
```
Body:
```json
{ "installments": [
  { "dueDate": "2026-04-15", "amount": 2000, "notes": null },
  { "dueDate": "2026-05-15", "amount": 2000, "notes": null },
  { "dueDate": "2026-06-15", "amount": 2000, "notes": null } ] }
```
Peşin → tek installment. Create line içinde de `paymentPlan` opsiyonel gönderilebilir.
**Yetki:** yazma+okuma Owner/StudioAdmin (D10). Öğrenci read-view (`/api/student/...`) faz-2.

---

## 11. Frontend

### 11.1 Sözleşme oluştur (`contracts/new`)
"Ödeme planı" bölümü (Ücret altından sonra):
- **Ödeme tipi:** Peşin | Taksitli (radio/segment).
- **Peşin:** tek "Ödeme tarihi" → `installments=[{dueDate, amount: computedTotal}]`.
- **Taksitli:** yöntem **Otomatik aylık** (taksit sayısı + ilk vade + her ay aynı gün) | **Özel tarihler** (satırlar: vade + tutar + sil/ekle).
- **Önizleme** her zaman: `15 Nis 2.000 TL / 15 May 2.000 TL / ...` + **toplam kontrolü** (Σ == computedTotal değilse buton disabled + "Toplam X, olması gereken Y").
- Build → `ContractLineInput.paymentPlan` (opsiyonel).

### 11.2 Sözleşme detayı (`ContractDetailModal`)
- "Ödeme Planı" bölümü: installment listesi + durum rozetleri (**Ödendi**/success, **Kısmi**/warning, **Gecikti**/danger, **Bekliyor**/neutral), `nextDue`, `overdueAmount` rozeti, "3 taksitin 1'i ödendi".
- "Planı düzenle" → aynı editör (PUT). Ödeme varsa uyarı.
- Plan yoksa "Ödeme planı yok — oluştur".

### 11.3 Tipler/servis
- `domain-types.ts`: `InstallmentInput`, `PaymentInstallmentDto`, `ContractLinePaymentPlanDto`, `InstallmentStatus` (`Planned|PartiallyPaid|Paid|Overdue`), label'lar. `ContractLineInput.paymentPlan?`.
- `studioApi.ts`: `contractService.getPaymentPlan(lineId)`, `updatePaymentPlan(lineId, installments)`.
- Plan editörü create + edit ortak bileşen; `money(n,cur)` tr-TR.

---

## 12. Migration / backward compatibility

- **Tek yeni tablo** `ContractLinePaymentInstallments` (+ index). Additive; **backfill YOK**.
- Plan olmayan line'lar **eski davranışla** çalışır (`line.DueDate` + payments + 28D reminder). UI "Ödeme planı yok".
- **Plan zorunlu DEĞİL (D7)** — yeni line plan'sız açılabilir → mevcut `addline` smoke'ları (28b/29a/...) **kırılmaz**. UI güçlü öneri. (Pilotta zorunlu yapmak istenirse ayrı tur; tüm legacy create çağıranları kırar.)
- `Payment` şeması **değişmez** → 28A/28B/29A borç matematiği aynen.

---

## 13. Test planı (spec'e dahil)

**A. Plan CRUD**
- Peşin → 1 installment (Amount=TotalPrice).
- 3 taksit otomatik aylık → 3 installment, Σ=TotalPrice.
- Özel tarihler → girilen satırlar.
- `Σ != TotalPrice` → 400.
- 0/negatif amount → 400.
- Yuvarlama: 10.000/3 → son taksit farkı, Σ tam.

**B. Allocation/status**
- Ödeme yok → hepsi Planned (geçmiş vade → Overdue).
- Kısmi ödeme → ilgili taksit PartiallyPaid; sonrakiler Planned.
- Tam/fazla ödeme → tüm taksit Paid + `overpaidAmount` > 0.
- Legacy line ödemesi → en eski vadeden dağıtılır (örn. 3.000 → 1.taksit Paid, 2. kısmi).

**C. Overdue**
- `DueDate < today` + ödenmemiş → Overdue; `today == DueDate` → Overdue değil.
- `nextDueDate/nextDueAmount` doğru.

**D. Create line**
- paymentPlan ile line → installment'lar + `line.DueDate=en erken vade`.
- paymentPlan'sız → plan yok, eski davranış.
- Peşin + ödeme tarihi → 1 installment.

**E. UI (manuel/checklist)**
- contracts/new ödeme planı (peşin/taksitli/otomatik/özel/önizleme/toplam kontrolü).
- ContractDetailModal plan listesi + rozetler + nextDue.
- Payment ekleme sonrası status güncellenir (GET yeniden hesaplar).

**F. Regression**
- `dotnet build` 0/0 · migration apply · `has-pending` clean · `tsc` 0 · `next build` 0.
- `sprint38-smoke` + **28a/28b/28d/23/24/29a** (payment/debt/reminder) + **34/35/37** (line/schedule/working-hours) **yeşil**.
- 28D reminder: plan kaydı `line.DueDate` senkronu sonrası reminder davranışı bozulmadı.

---

## 14. Açık sorular (karar/öneri)

1. **Reminder:** MVP'de `line.DueDate = en erken taksit vadesi` senkronu (28D kod değişmez) — yoksa `ReminderScanner`'ı taksit-bazlı (her taksit için ayrı reminder) mı yapalım? (Öneri: **MVP senkron**; taksit-bazlı reminder faz-2.)
2. **Öğrenci read-view:** taksit planını öğrenci görsün mü? (Öneri: faz-2; MVP staff-only.)
3. **Plan zorunluluğu:** yeni line'da plan zorunlu mu? (Öneri: **opsiyonel** — eski akış/smoke kırılmasın.)
4. **Fazla ödeme:** `overpaidAmount` yalnız gösterilsin, otomatik iade/credit yok (mevcut Refunded dormant). (Öneri: göster, aksiyon yok.)
5. **Otomatik taksit üretimi:** frontend mi (açık liste), backend "generate" modu mu? (Öneri: **frontend üretir**, backend açık liste + Σ doğrular.)
6. **Plan düzenleme ödeme varken:** serbest+uyarı mı, kısıtlı mı? (Öneri: **serbest+uyarı**; allocation güvenli.)
7. **Faz:** tek sprint (tablo+servis+API+create/PUT+UI) mi? (Öneri: **tek sprint**, tek migration.)

---

## 15. Önerilen ilk uygulama kapsamı (onay sonrası, ayrı turda)

Backend: `ContractLinePaymentInstallment` entity + config + **migration `Sprint38_ContractLinePaymentInstallments`** (additive); `DbSet`; `PaymentInstallmentPlanService` (allocation/durum hesabı, `line.Paid` üzerinden); `GetContractLinePaymentPlanQuery` + `UpdateContractLinePaymentPlanCommand` (full-replace, validator: Σ==TotalPrice, amount>0) + `CreateStudentContractLineCommand`'a opsiyonel `PaymentPlan`; 2 controller endpoint; `line.DueDate` senkronu.
Frontend: contracts/new "Ödeme planı" (peşin/taksitli/otomatik/özel/önizleme); `ContractDetailModal` plan listesi + rozet + düzenle; tipler + `getPaymentPlan/updatePaymentPlan`.
Doğrulama: build 0/0 · tsc/next 0 · migration apply + has-pending clean · `sprint38-smoke` (A–D) · regresyon (28a/28b/28d/23/24/29a + 34/35/37). Commit/tag yalnız ayrı onayla (öneri tag faz sonunda **v1.29.0-pilot**).

> **Bu spec analiz turudur. Şu an kod/migration/commit YOK.** Uygulama yalnız onaylanan kararlarla başlar.
