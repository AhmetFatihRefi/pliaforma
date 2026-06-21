# SPRINT 28D — SPEC: Line Due-Date Reminder + Notification Throttle

> Durum: ONAYLI — uygulanıyor. Migration YOK (beklenti). Commit/push ayrı onayla.
> Zemin: 28B line payment/debt (her line'da `DueDate` nullable + `Remaining` hesaplı); 28A/C line + makeup line-based (v1.6.0-pilot).
> Amaç: `StudentContractLine.DueDate` dolu + `Remaining>0` + vade bugün/geçmiş → öğrenciye **nazik** otomatik in-app bildirim; tümü aynı anda basılmasın (throttle/stagger).
> Kapsam dışı: gerçek email/SMS, online ödeme, fatura, installment, late fee, ödeme linki, `actionUrl` kolonu,
> `RelatedStudentContractLineId` kolonu, staff dashboard, ClassType/Amendment drop, drag/drop.

## 0. Açık soru kararları (onaylı)

1. **Dedup granülü = öğrenci-bazlı haftalık ÖZET.** Aynı öğrenciye aynı ISO haftasında **TEK** `LinePaymentDue`; çoklu line tek body'de gruplanır (her line ayrı satır). Per-line ayrı bildirim YOK.
   DedupKey: **`line-payment-due:{studentProfileId}:{isoYear}-W{isoWeek}`** (StudioId unique index'te örtük).
2. **Haftalık tekrar kanıtı = SQL ile dedup key'i önceki haftaya çek** (sistem tarihi değiştirilmez). İlk trigger üretir; aynı hafta 2. trigger duplicate üretmez; SQL ile mevcut key geçen-hafta'ya güncellenince yeni trigger current-week için yeni bildirim üretir.
3. **Misafir öğrenci (UserId NULL) → in-app reminder ÜRETİLMEZ** (skip). Staff'a ayrı sinyal yok; `SkippedGuest` sayısı result/log'a yazılabilir. Borç finans ekranlarında görünmeye devam eder.
4. **Config defaults:** `Notifications:LineReminderMaxPerStudioPerRun = 50`, `Notifications:InterStudioDelayMs = 0`. Smoke'ta cap 1'e çekilip throttle kanıtı üretilir.
5. **Cadence = tek model `LinePaymentDue`** (due today + overdue aynı model, haftalık dedup). Ayrı "due today"/"overdue" tipi YOK.
   Koşul: `Status=Active` + `DueDate != null` + `DueDate <= studioLocalToday` + `Remaining > 0`.

---

## 1. Mevcut notification/reminder altyapısı analizi

**Notification entity:** `StudioId`, **`RecipientUserId` (ZORUNLU)**, `Type`, `Channel`, `Status`, `Title`, `Body`, `DeduplicationKey`,
`RelatedClassSessionId/ClassBookingId/PaymentId`, zaman damgaları. **`RelatedStudentContractLineId` / `actionUrl` YOK.**

**Migration-bağımsızlık (kritik):** `NotificationConfiguration` → `Type` **`HasConversion<string>(32)`**, `DeduplicationKey string(200)`,
unique index **`(StudioId, DeduplicationKey)` filtered `IsDeleted=false`**. `NotificationEnums` yorumu doğruluyor: enum sonuna değer
eklemek **migration gerektirmez** (string saklama; MembershipInvite=7 deseni).

**ReminderScanner.RunForStudioAsync:** tek stüdyo için 3 kaynak (ClassReminder + PaymentDueSoon + PaymentOverdue) — **hepsi Payment satırı bazlı**;
DedupKey ön-kontrolüyle var olanları eler, dispatch eder, **bulk insert**; **throttle/limit YOK**.

**Worker (ReminderBackgroundService):** `Notifications:ReminderIntervalSeconds` (vars. 300), `Notifications:WorkerEnabled` (dev'de kapalı →
manuel trigger). Aktif stüdyoları **sırayla** dolaşır (doğal stagger); per-run/per-studio bildirim limiti YOK.

**Manuel trigger:** `POST /api/studio/notifications/run-reminders` (Owner/StudioAdmin) → `RunStudioRemindersCommand` → scanner. Smoke determinizmi.

**Student uçları:** `GET /api/notifications` (kendi, son 50) + unread-count + read + read-all. **Deep-link YOK.** **Misafir (UserId NULL) → bildirim üretilmez.**

**Line remaining (28B):** `ContractLineQuery.BuildAsync/ComputeDebtAsync` her line için `Remaining = TotalPrice − paid` (legacy-fallback paid,
currency line'ın; unassigned ödeme borçtan düşmez; cross-currency yok) verir → 28D `remaining>0` koşulu buradan.

---

## 2. Önerilen line reminder modeli

`ReminderScanner`'a **4. kaynak** (Payment-bazlı 3 kaynak korunur — geri uyum):
- Aday line: `StudentContractLine` `Status=Active` + `DueDate != null` + `DueDate <= today` (stüdyo-yerel `SessionRules.StudioNowAsync`).
- `remaining`: aday line kümesi `ContractLineQuery.BuildAsync` (28B) ile DTO'ya → `Remaining > 0` olanlar (currency line'ın).
- Alıcı: line'ın öğrencisi → `StudentProfile.UserId`; **UserId NULL → atla** (`SkippedGuest++`).
- `Status != Active` (Cancelled/Completed) / `DueDate NULL` / `Remaining<=0` → eler.
- **Öğrenci bazında grupla** → her öğrenci için **TEK** haftalık özet.

## 3. Dedup modeli

- DedupKey: `line-payment-due:{studentProfileId}:{isoYear}-W{isoWeek}` (`System.Globalization.ISOWeek.GetYear/GetWeekOfYear(today)`).
- Bir öğrenci/hafta = 1 bildirim. Aynı hafta 2. tarama → key var → üretilmez. Sonraki ISO-hafta → yeni key → tekrar (borç açıksa).
- Mevcut idempotency yolu (DedupKey ön-kontrol + unique index) aynen kullanılır; line kaynağı candidate listesine eklenir.

## 4. Throttle modeli

- **Per-studio per-run cap** (`LineReminderMaxPerStudioPerRun`, vars. 50): bir taramada en çok N **öğrenci özeti**; fazlası **bu turda atlanır**
  (`Deferred` sayısı + log). DedupKey yazılmadığı için **sonraki run (aynı hafta) kalanları tamamlar** (silent truncation YOK → log).
- Öğrenci-bazlı özet = en büyük throttle (N line → 1 insert).
- Sıralama: en eski `DueDate` (en geciken) önce.
- **Stagger:** worker stüdyoları sırayla işler (mevcut). Opsiyonel `InterStudioDelayMs` (vars. 0).
- Aynı anda yüzlerce insert YOK (özet + cap).

## 5. Notification type

- **`NotificationType.LinePaymentDue` (= 8).** String saklama → **migration YOK**. Eski `PaymentDueSoon/PaymentOverdue` (Payment-bazlı) korunur.
- `ReminderRunResultDto` → `+ LinePaymentDue` (+ `Deferred`, `SkippedGuest` sayıları; Total güncellenir).

## 6. Bildirim metni (nazik)

Title: **"Ödeme hatırlatması"**.

Tek line body:
> Merhaba, sistemimizde **Mat Pilates** paketiniz için ödenmesi beklenen **750 TRY** tutarında bir ödeme görünüyor. Ödemenizi yaptıysanız lütfen bu mesajı dikkate almayınız.

Çoklu line body:
> Merhaba, sistemimizde aşağıdaki paketleriniz için ödenmesi beklenen tutarlar görünüyor:
> - Mat Pilates · 01.02.2026–01.04.2026 · kalan 750 TRY · son ödeme 15.03.2026
> - Reformer · 01.02.2026–01.04.2026 · kalan 500 TRY · son ödeme 20.03.2026
>
> Ödemenizi yaptıysanız lütfen bu mesajı dikkate almayınız.

Sert dil YOK; "borcunuzu ödeyin" YOK; **"Ödemenizi yaptıysanız lütfen bu mesajı dikkate almayınız."** mutlaka.

## 7. Student / Staff görünümü

- **Student:** mevcut `GET /api/notifications` + bell yeni tipi gösterir (ek UI gerekmez). Body line bilgisini içerir. Deep-link YOK (Body sözel yönlendirme).
- **Frontend:** notification tip label'ı gerekiyorsa **"Ödeme hatırlatması"** (`LinePaymentDue`). actionUrl/migration YOK.
- **Staff:** ayrı dashboard YOK; audit = scanner log + `Notifications` satırları + result sayıları.

## 8. Migration gerekir mi?

**Beklenti: HAYIR.** Enum string-saklama (migration yok); dedup mevcut `DeduplicationKey` + unique index yeterli; throttle config + scanner-içi;
line bilgisi Body + DedupKey'de. **DUR-VE-RAPORLA (kod yazmadan):** `RelatedStudentContractLineId` / `actionUrl` kolonu, throttle tablosu,
dedup kolonu, tip için DB enum, veya zorunlu yeni composite index gerekirse → dur, raporla, onay iste. Başta `has-pending` doğrula.

## 9. Test planı — `sprint28d-smoke.sh`

Worker dev'de kapalı → manuel `run-reminders` ile deterministik.
- DueDate NULL → bildirim YOK.
- Remaining 0 (tam ödenmiş) → bildirim YOK.
- Cancelled/Completed line → bildirim YOK.
- DueDate bugün + remaining>0 → `LinePaymentDue` OLUŞUR (nazik metin + "dikkate almayınız").
- DueDate geçmiş + remaining>0 → OLUŞUR.
- Aynı öğrenci aynı hafta 2. trigger → **duplicate YOK**.
- SQL ile dedup key önceki haftaya çekilince → tekrar trigger → current-week için yeni bildirim.
- Line ödemesi sonrası remaining 0 → yeni bildirim YOK.
- Unassigned (line'sız) ödeme remaining'i düşürmez → reminder hâlâ oluşur.
- Çok line aynı öğrenci → **tek özet** (her line body'de satır).
- Per-run cap=1 + 2 öğrenci → 1. run 1 bildirim, 2. run kalan 1.
- Student kendi görür; **başka student GÖREMEZ**; misafir (UserId NULL) → üretilmez.
- **Regresyon:** eski ClassReminder + PaymentDueSoon/Overdue çalışır; v1.6 makeup / v1.5 scheduling / v1.4 debt korunur.

**Doğrulama:** backend 0/0 · tsc/next 0 · migration NONE + has-pending No changes · smoke suite (34→35) yeşil · E2E yeşil · full compose.
Commit/push/tag YOK. Yeşilse tag önerisi v1.7.0-pilot.

## 10. Riskler

1. **Recipient=User zorunlu:** misafirler reminder alamaz (borç dashboard'da görünür).
2. **Remaining maliyeti:** `BuildAsync` line başına alt-sorgu; `DueDate<=today` filtresi + cap yükü sınırlar. `(StudioId,Status,DueDate)` index 28A'da var mı doğrulanır; yoksa DUR-RAPORLA.
3. **ISO-hafta dedup determinizmi:** smoke key'i SQL ile kurar.
4. **Worker açıkken spike:** cap + özet engeller (28D çekirdeği).

## 11. Önerilen uygulama sırası

1. `NotificationType.LinePaymentDue` + `ReminderRunResultDto` (LinePaymentDue/Deferred/SkippedGuest).
2. `ReminderScanner` line kaynağı: aday line → `BuildAsync` remaining → UserId çözümü → öğrenci grupla → haftalık dedup + nazik özet body + per-studio cap.
3. Worker `InterStudioDelayMs` (vars. 0) + cap config okuma.
4. Frontend notification label (gerekirse).
5. `sprint28d-smoke` + full compose doğrulama.

Yeşil uygulanırsa sonraki tag önerisi: **v1.7.0-pilot**.
