# SPRINT 28 — SPEC & ANALİZ: Contract Lines, Simplified Class Types & Line-Based Makeup Rights

> **Durum:** Onaylandı; **fazlara bölündü**. Bu turda yalnız **Sprint 28A** uygulanır (28B/C/D ayrı turlarda).
> **Onaylanan kararlar:** (1) fazlı (sözleşme/ödeme/telafi/program AYNI ANDA değişmez). (2) Mevcut sözleşme =
> header + 1 line; haklar/ücret/telafi LINE üzerinden. (3) Yeni alım = yeni line; `ContractAmendment` dormant.
> (4) Telafi LINE-bazlı zorunlu (28C); eski bağlanamayan dev ledger temizlenir/soft-delete. (5) Backfill
> `DueDate=NULL` (EndDate proxy YOK). (6) ClassType alanları dormant (drop yok). (7) PricePerLesson otoriter,
> `TotalPrice = LessonCount × PricePerLesson`.

## Context

`StudentContract` bugün tek (ders tipi + hoca + tarih + ders sayısı + ücret + telafi) birimi. Gerçekte öğrenci aynı
anda farklı paketler (Mat + Reformer) alır; her birinin ayrı tarihi/ücreti/telafisi/son-ödeme tarihi olur; sonradan
"10 ders daha" alımı eski paketi şişirmemeli. Seviye aslında öğrencinin özelliği. Epic, sözleşmeyi **line-tabanlı**
kuruyor: header + çok line; hak/ücret/telafi/son-ödeme/bireysellik line düzeyinde; telafi kredisi doğduğu line'a
bağlı; normal ders line tarih aralığında, telafi dersi line bitişinden sonra da yapılabilir; son ödeme tarihinde
nazik+throttle'lı hatırlatma.

---

## Mevcut model (kod-temelli özet)
- **Borç STORED DEĞİL** (`ContractDebtService`): `Price + Σ amendment.AdditionalPrice − Σ Paid-payment`. → "ödenen
  stored olmasın" zaten böyle.
- `ContractAmendment` (Sprint 23) contract'ı canlı mute eder → "yeni alım = yeni line" ile **dormant**.
- `Payment.StudentContractId` (nullable FK SetNull) tek bağ; null=etiketsiz.
- `GenerateContractSchedule`: `StartDate..EndDate` aralığı; `available = TotalLessonCount − reserved`;
  `IsIndividualDefault → ClassSession.IsIndividual`; group 9999 / individual 1 (**MaxCapacity kullanılmaz**).
- Telafi: `MakeupCreditTransaction` **öğrenci-düzeyinde, line/contract linki YOK**; `MakeupAllowance` yalnız
  `AttendanceMarker` (AbsentEarned cap). `MakeupRequest` (Sprint 27) line linki YOK.
- ClassType: `MaxCapacity` yalnız `CreateClassSession`/`BulkGenerate` default kapasitesinde okunur; Level/Purpose/
  Equipment display-only. `StudentProfile.Level` YOK.
- Bildirim: `Notification.DeduplicationKey` + `PaymentDueSoon`/`PaymentOverdue` zaten var; per-recipient throttle yok.

---

## Yeni model
- **`StudentContractLine`** (tenant-scoped): `StudentContractId(header), ClassTypeId, InstructorProfileId, StartDate,
  EndDate, LessonCount, PricePerLesson, TotalPrice(=count×perLesson), MakeupAllowance, DueDate(nullable), IsIndividual,
  Status, Currency, SequenceNo, Notes`. Paid/Remaining **hesaplanır** (Σ Paid payment on line).
- **`StudentContract` → header**: canlı = `StudentProfileId, Status, Notes`. Satır-alanları **dormant**.
- 4 nullable FK: `Payment/ClassBooking/MakeupCreditTransaction/MakeupRequest .StudentContractLineId`.
- **`StudentProfile.Level`** (nullable `StudentLevel{Beginner,Intermediate,Advanced}`).
- ClassType `Equipment/CapacityType/MaxCapacity/Level/Purpose` UI/DTO'dan çıkar, **kolonlar dormant**.

---

## Faz planı (onaylı)
- **28A** — veri modeli + migration + backfill + read-model + ClassType sadeleştirme + Student.Level. **Davranış değişmez.**
- **28B** — line payments/debt + sözleşme UI line editör (line ekle = yeni alım).
- **28C** — scheduling/booking/makeup line-based + individual hard rule + seviye filtresi + tarih-aralığı kuralı.
- **28D** — line due-date reminder + notification throttling.

---

## Sprint 28A kapsamı (bu tur)

**Backend:** `StudentContractLine` entity+config; `StudentContractLineStatus` + `StudentLevel` enum; `StudentProfile.Level`;
4 nullable FK; migration `Sprint28A_ContractLines` (additive) + **backfill SQL**; line read-model + DTO; staff +
student read-only line endpoint; ClassType DTO/validator trim; `CreateClassSession`/`BulkGenerate` MaxCapacity→grup
default (9999); Student create/update DTO'larına Level. **Dual-write (geçiş modu, davranış değişmez):**
CreateStudentContract → 1 line; booking create (BookingCreator/Generate/GroupChange) → `StudentContractLineId`;
CreatePayment(contractId) → lineId; amendment → contract'ın line'ını senkronla; AbsentEarned → booking'in lineId'i.

**Backfill (migration + re-runnable script):** her contract → 1 line (`DueDate=NULL`, `TotalPrice=Price+Σamendment`,
`PricePerLesson=TotalPrice/max(count,1)`); Payment/ClassBooking → contract'ın line'ı; MakeupCreditTransaction →
`SourceBookingId`/`UsedForBookingId`→booking→line; MakeupRequest → eşleşen aktif line; **bağlanamayan dev makeup
ledger → soft-delete (IsDeleted)**; rapor (bağlanan/bağlanamayan sayıları).

**Frontend:** class-types formu+tablosu sadeleşir (equipment/capacityType/maxCapacity/level/purpose yok); öğrenci
formuna Level; öğrenci detayında contract **line read-only** listesi. (Line editör / line ekleme / payment-line /
scheduling / makeup enforcement / reminder YOK.)

**Kapsam dışı (28A):** line ekleme UI, line ödeme/borç yönetimi, line scheduling, line makeup enforcement, individual
hard rule, seviye filtresi, due-date reminder, throttle, ContractAmendment drop, ClassType kolon drop.

---

## Açık soru kararları (uygulanan)
1. Backfill `DueDate=NULL`; reminder (28D) yalnız DueDate dolu line'larda.
2. Yeni alım: aktif header'a line; yoksa yeni header; çok header → UI seçer; default en güncel aktif (28B).
3. PricePerLesson otoriter; TotalPrice=count×perLesson; manuel override yok.
4. Reminder haftalık+nazik+kovalı dedup+per-run throttle (28D).
5. Eski makeup ledger: bağlananı bağla, bağlanamayanı soft-delete; NULL fallback YOK; 28C'de line zorunlu. 28A raporu:
   bağlanan / bağlanamayan / yapılan işlem.
6. ClassType alanları dormant; UI/DTO/validator'dan çıkar; ileride ayrı cleanup'ta drop.
7. Line seçimi: booking varsa `booking.StudentContractLineId` esas; absence kredisi booking'in line'ına; yeni regular
   resolver → classType+instructor+tarih+available uyan **en erken biten aktif line**; makeup line seçimi 28C.

---

## Doğrulama (28A)
backend build 0/0 · frontend tsc 0 · next build 0 · migration apply + has-pending No changes · **backfill raporu**
(her contract=1 line, payment/booking 100% bağlı, ledger bağlanan/soft-delete sayısı) · `sprint28a-smoke` yeşil ·
smoke suite (30→31) yeşil · E2E (60) yeşil · full compose. Commit/push/tag yalnız ayrı onayla (öneri: `v1.3.0-pilot`).
