# Telafi Kredileri, Erteleme ve Toplu Kapanış (Sprint 5)

## MakeupCredit neden ledger (defter) olarak tutuluyor?
Tek bir tamsayı `balance` alanı "kim, neden, ne zaman" sorularını yanıtlayamaz ve denetlenebilir
değildir. Bunun yerine her kredi olayı ayrı bir **`MakeupCreditTransaction`** kaydıdır (Granted, Used,
Restored, Revoked, Expired). Böylece bir kredinin nereden geldiği (`SourceClassSessionId`,
`SourceBookingId`, `Reason`), kim tarafından verildiği (`CreatedByUserId`) ve nerede tüketildiği
(`UsedForBookingId`) izlenebilir. Hareketler **soft delete edilmez**; yanlış işlem için ters bir hareket
(`Revoked`/`Restored`) eklenir — defterin bütünlüğü korunur.

## Balance (bakiye) nasıl hesaplanıyor?
**Bakiye = aktif (silinmemiş) hareketlerin işaretli `Amount` toplamı.** İşaret kuralı: `Granted`/`Restored`
pozitif, `Used`/`Revoked`/`Expired` negatif. `Amount` asla 0 olamaz. Hesap `MakeupCreditService.GetBalanceAsync`
içinde tek bir `SUM(Amount)` sorgusudur — ayrı bir sayaç tutulmaz (defter tek doğruluk kaynağıdır).

## Session cancel krediyi nasıl etkiliyor? (çift kredi YOK)
İptalde her rezervasyon için **tek bir kredi sonucu** uygulanır — restore VE grant birlikte oluşmaz:

- **Booking bir telafi kredisi tükettiyse** (yani Makeup seansı rezervasyonuydu): yalnızca **tüketilen kredi
  geri verilir** (`Restored`, `Reason=MakeupBookingCancelled`). `grantMakeupCredit=true` olsa bile **ekstra +1
  verilmez**. Örnek: bakiye 1 → makeup booking → 0 → session cancel (grant=true) → **bakiye 1** (2 değil).
- **Booking kredi tüketmediyse** (Regular seans) ve `grantMakeupCredit=true` ise: sahibine **+1** telafi
  kredisi (`Reason=StudioCancelledClass`) verilir. **Aynı seans+öğrenciye iki kez verilmez:** uygulama
  kontrolü (`HasGrantForSessionAsync`) + DB filtered unique index `(StudentProfileId, SourceClassSessionId)
  WHERE Type='Granted'`.

Bu ayrım `SessionCancellationService` içinde `RestoreConsumedMakeupCreditIfAnyAsync` (tüketim varsa true döner)
ve `GrantCreditForStudioCancellationIfEligibleAsync` (yalnızca tüketmemiş booking'ler için çağrılır) metotlarıyla
nettir. **İleride** "ekstra telafi/tazminat kredisi" istenirse, makeup booking'lere de uygulanan **ayrı bir
alan/aksiyon** olarak tasarlanır — bu sürümde yoktur.

## Reschedule ile cancel farkı nedir?
- **Reschedule** (`POST .../reschedule`): seans **iptal edilmez**, zamanı değişir. `keepBookings=true` ise
  mevcut rezervasyonlar seansla birlikte taşınır (korunur). `keepBookings=false` ise aktif rezervasyonlar
  `CancelledByStudio` yapılır (isteğe bağlı telafi kredisiyle) ve seans yeni zamanıyla **boş** devam eder.
  Yeni zaman için geçmiş-zaman, hoca çakışması ve müsaitlik kuralları (Sprint 3) tekrar uygulanır. Erteleme
  izi `RescheduledAt`/`RescheduleReason` ile saklanır.
- **Cancel**: seans `Cancelled` olur, aktif rezervasyonlar `CancelledByStudio` yapılır, `BookedCount=0`.

## Bulk closure (toplu kapanış) ne yapar?
`StudioClosure`, verilen tarih aralığındaki **planlı** seanslara tek seferlik toplu bir aksiyon uygular
(opsiyonel hoca/grup filtresiyle):
- `CancelSessions` — seansları iptal eder.
- `CancelAndGrantMakeupCredit` — iptal eder + rezervasyon sahiplerine `Reason=HolidayClosure` kredisi verir.
  **Aynı çift kredi kuralı geçerlidir:** makeup kredisi tüketmiş booking yalnızca **restore** edilir
  (ekstra grant yok), yalnızca kredi tüketmemiş (Regular) booking'ler +1 alır.
- `RescheduleByOffset` — seansları `OffsetDays` kadar ileri alır; geçmişe düşen / çakışan / müsaitlik dışı
  kalanlar **atlanır** (`skipped`, neden ile). Aynı ofsetle kayan seanslar kendi aralarında çakışmaz; çakışma
  yalnızca batch dışı seanslarla kontrol edilir.

Sonuç: `affected/cancelled/credited/rescheduled/skipped` sayıları kayıtta saklanır ve POST yanıtında her
seansın sonucu (`outcome`) döner. **Bu recurring bir sistem DEĞİLDİR** — yalnızca o anda mevcut seanslar
üzerinde toplu operasyondur. Otomatik resmi tatil entegrasyonu yoktur; tatiller manuel closure olarak girilir.

## Makeup session nedir?
`ClassSession.SessionKind` alanı `Regular | Makeup | Trial | Special` değerlerini alır (varsayılan
`Regular`). **Makeup** seansı normal kapasiteye sahiptir ama rezervasyonu **1 telafi kredisi tüketir**.
`Trial`/`Special` bu sürümde yalnızca etikettir (özel davranış yoktur).

## Makeup booking kredi tüketimini nasıl yapıyor?
`BookingCreator`, seans `Makeup` ise: önce öğrenci satırını kilitler (`IMakeupCreditLock`, `FOR UPDATE`),
bakiyeyi okur; bakiye > 0 ise `Used` (−1, `UsedForBookingId=booking.Id`) hareketi ekler. **Kredi tüketimi +
booking insert aynı transaction'dadır** (`TransactionBehavior`) — booking insert patlarsa kredi de geri
alınır (rollback), "kredi düştü ama booking olmadı" durumu oluşmaz. Bakiye yoksa **409** döner. (İstisna:
politika `AllowMakeupBookingWithoutCreditByAdmin=true` ve booker Owner/StudioAdmin ise kredisiz override —
varsayılan kapalıdır, güvenli varsayım 409.) Bir booking için iki kez kredi tüketimini DB seviyesinde
**filtered unique index** `(UsedForBookingId) WHERE NOT NULL` engeller.

> **Expiry notu:** Rezervasyon sırasında kredinin `ExpiresAt`'i **KONTROL EDİLMEZ**. Bu sürümde bakiye>0
> yeterlidir; süresi geçmiş görünen bir kredi de kullanılabilir. Expiry zorlaması ileriki bir sprinttedir.

## Kredi restore ne zaman oluyor?
Bir **makeup** rezervasyonu iptal edilirse (öğrenci self iptal, staff iptal veya seans cancel/reschedule-drop
cascade'i), tüketilen kredi **`Restored` (+1, `Reason=MakeupBookingCancelled`)** hareketiyle geri verilir.
Çift iade engeli: yalnızca booking için bir `Used` hareketi varsa ve henüz `Restored` yoksa iade yapılır;
booking yeniden iptal edilemediğinden (status guard) ikinci iade oluşmaz.

## Hocaların manuel kredi verme sınırı nedir?
- Instructor manuel kredi verebilmesi politikaya bağlıdır (`AllowInstructorManualGrant`, vars. `true`).
  Kapalıysa → 403.
- Instructor yalnızca **kendi oluşturduğu** ya da **kendi seanslarında rezervasyonu olan** öğrencinin
  kredisini yönetir (`MakeupCreditAccess`). Owner/StudioAdmin tüm öğrencileri yönetir.
- Öğrenci yalnızca **kendi** bakiyesini ve geçmişini görür (`GET /api/student/makeup-credits`).

## Aylık kredi limiti nasıl ele alındı? (enforce edilen)
`StudioMakeupPolicy.MonthlyManualCreditLimit` (vars. **2**) **yalnızca manuel grant'lere** uygulanır:
bir öğrenciye bir **takvim ayında** (UTC ay başı) verilebilecek `ManualByOwner`/`ManualByInstructor` toplamı
bu limiti aşamaz → aşılırsa 409. **Stüdyo iptali / tatil kapanışı kredileri bu limite TAKILMAZ** (otomatik
telafi). Limit, manuel grant yapan herkese (Owner dâhil) uygulanır — stüdyonun kendi politikasıdır.

## Bu sürümde bilinçli ele alınmayanlar (dürüst not)
- **Kredi geçerlilik süresi (ExpiresAt) ZORLANMAZ — yalnızca bilgilendirici / gelecek altyapısı.** Grant
  sırasında `DefaultCreditValidityDays` (vars. 30) ile `ExpiresAt` **hesaplanıp saklanır**, ancak:
  (1) bakiyeden düşülmez, (2) otomatik `Expired` hareketi üretilmez, (3) **rezervasyon sırasında kontrol
  edilmez**. Yani "kredi süresi dolduysa kullanılamaz" şu an **GEÇERLİ DEĞİLDİR**. `ExpiresAt` ileride
  expiry job devreye girdiğinde anlam kazanacak bir veri alanıdır. Frontend bu sürümde öğrenciye `ExpiresAt`'i
  göstermez (yanlış izlenim verilmesin diye). Tam expiry mantığı (arka plan job + bakiye dışlaması) ileriki
  bir sprinttedir.
- **Geç iptal politikası / no-show OTOMASYONU YOK.** `StudentCancelledInTime` enum'da yer alır ama otomatik
  tetiklenmez. **Sprint 6 (yapıldı):** yoklamadan **manuel** telafi kredisi verilebilir — Excused →
  `ExcusedAbsence`, LateCancel → `NoShowApproved` reason'larıyla; her zaman manuel seçim (otomatik değil) ve
  yoklama başına bir kez. Detay: [attendance.md](attendance.md). Otomatik geç-iptal/no-show **cezası/kredisi**
  hâlâ yoktur.
- Bu sprintte ayrıca: ödeme/paket düşme, bildirim worker, recurring booking, bekleme listesi,
  otomatik resmi tatil entegrasyonu **yapılmadı**.

## Yetki & tenant özeti
Tüm entity'ler (`MakeupCreditTransaction`, `StudioClosure`, `StudioMakeupPolicy`) `StudioId == token.StudioId`
ile yüklenir → başka stüdyo **404**. Body'den gelen StudioId'ye güvenilmez. Closure ve politika güncelleme
**Owner/StudioAdmin**'e kısıtlıdır; bulk closure stüdyo geneli bir işlem olduğundan Instructor'a kapalıdır.

## Endpoint özeti
| Endpoint | İşlev |
|---|---|
| `POST /api/studio/class-sessions/{id}/reschedule` | seans ertele (keepBookings, opsiyonel kredi) |
| `POST /api/studio/class-sessions/{id}/cancel` | seans iptal (reason, grantMakeupCredit) |
| `GET/POST /api/studio/closures`, `GET .../{id}` | toplu kapanış listele / uygula / tekil |
| `GET /api/studio/students/{id}/makeup-credits` | öğrenci bakiye + geçmiş (staff) |
| `POST .../makeup-credits/grant` · `.../revoke` | manuel kredi ver / geri al |
| `GET/PUT /api/studio/makeup-policy` | politika oku / güncelle (Owner/StudioAdmin) |
| `GET /api/student/makeup-credits` | öğrenci kendi bakiye + geçmişi |

Manuel doğrulama: `scripts/sprint5-smoke.sh` (makeup booking kredi tüketimi/iade, no-credit 409, manuel
grant/limit/revoke, cancel+grant, reschedule keep/drop, bulk closure cancel+grant ve offset skip, tenant +
rol izolasyonu).
