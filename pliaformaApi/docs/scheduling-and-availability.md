# Hoca Müsaitliği ve Ders Saatleri (Sprint 3)

## InstructorAvailability neden ayrı entity?
Müsaitlik, hocanın **haftalık tekrar eden** çalışma penceresidir (örn. her Salı 09:00–12:00) ve somut
bir derse bağlı değildir. Ders seansları bu pencerelerin içine düşmek zorunda olduğundan (hoca için
tanımlıysa), müsaitlik bağımsız ve yeniden kullanılabilir bir kayıt olarak tutulur. Tenant-scoped.

## ClassSession nedir?
Takvimdeki **somut bir ders seansı**: bir hoca (`InstructorProfile`) + bir pilates grubu (`ClassType`),
belirli bir `StartAt`–`EndAt` aralığında. Sprint 4 rezervasyon sisteminin bağlanacağı birimdir.

## ClassSession ile rezervasyon farkı nedir?
ClassSession yalnızca **arz**ı (hangi ders ne zaman açık) temsil eder. Öğrencinin bir seansa
**rezervasyon** yapması, kapasite tüketimi, yoklama vb. **bu sprintte YOKTUR** (Sprint 4). `Capacity`
alanı yalnızca ileride rezervasyon sınırı olarak kullanılmak üzere tutulur; bu sprintte tüketilmez.

## EndAt nasıl hesaplanır?
`EndAt = StartAt + ClassType.DefaultDurationMinutes`. İstemci yalnızca StartAt verir; bitiş sunucuda
hesaplanır (asla body'den alınmaz). `Capacity` verilmezse **`ClassType.MaxCapacity`** kullanılır (en az 1).
> Not: ClassType'ta ayrı bir "DefaultCapacity" alanı yok; varsayılan kapasite `MaxCapacity`'dir.

## Availability yoksa davranış nedir?
- Hocanın **hiç** aktif müsaitliği yoksa: **Owner/StudioAdmin** yine ders açabilir; **Instructor**
  kendi adına ders açarken reddedilir (önce müsaitlik tanımlamalı) → 409.
- Müsaitlik **varsa**: seansın `[StartAt, EndAt]` aralığı, seansın gününe ait bir müsaitlik penceresine
  tamamen sığmalıdır; aksi halde 409 ("müsaitlik penceresi dışında").

## Çakışma kontrolü nasıl yapılır?
Aynı `InstructorProfile` için, **Scheduled** durumundaki başka bir seansla zaman aralığı çakışırsa
(yarı-açık aralık: `existing.StartAt < new.EndAt && new.StartAt < existing.EndAt`) reddedilir → 409.
**Cancelled** seanslar çakışma kontrolünde **dikkate alınmaz**. Güncellemede kendi kaydı hariç tutulur.
Ayrıca **geçmiş** zamana yeni seans açılamaz (Owner/Admin dahil).

## Bulk generate nasıl çalışır?
`POST /api/studio/class-sessions/bulk-generate` tek gün / tek aralık içinde, `ClassType` süresi (+
opsiyonel `breakMinutes`) ile **ardışık** seanslar üretir. **Recurring booking DEĞİLDİR.** Her slot için:
geçmiş → atla (`past`), çakışma → atla (`conflict`), müsaitlik dışı → atla (`outside-availability`).
Sonuç `{ created: [...], skipped: [{startAt, reason}] }` döner (çakışan oluşturulmaz).

## Timezone şimdilik nasıl ele alındı?
**Karar:** `StartAt/EndAt` stüdyo-**yerel duvar saati** olarak `timestamp without time zone` ile saklanır;
istemci stüdyo-yerel zaman gönderir, **UTC↔TZ dönüşümü Sprint 3'te yapılmaz**. Müsaitlik gün/saat
kontrolleri doğrudan StartAt'tan türetilir. Tek istisna: "geçmiş zaman" kontrolünde `Studio.TimeZoneId`
ile şimdiki yerel saat hesaplanır (hafif, tek dönüşüm). Gece yarısını aşan seanslar bu sürümde
desteklenmez (müsaitlik kontrolü gün içi varsayar). Tam UTC/TZ modeli ileride ele alınacaktır.

## Sprint 4'e nasıl zemin hazırladı? (yapıldı)
`ClassBooking` `ClassSessionId`'ye bağlandı; kapasite `ClassSession.BookedCount` ile atomik tüketiliyor.
ClassSession DTO'suna `bookedCount/remainingCapacity/isFull` eklendi. **Session iptal edilince** aktif
rezervasyonlar `CancelledByStudio` yapılır ve `BookedCount=0` set edilir (cascade). Detay:
[bookings-and-capacity.md](bookings-and-capacity.md).

## Erteleme, iptal ve seans türü (Sprint 5 — yapıldı)
- **SessionKind:** `ClassSession.SessionKind` (`Regular|Makeup|Trial|Special`, vars. Regular). **Makeup**
  seansı rezervasyonu 1 telafi kredisi tüketir; Trial/Special yalnızca etikettir.
- **Reschedule** (`POST .../{id}/reschedule`): yalnızca **Scheduled** seans ertelenir (aksi 409). Yeni
  zaman için **geçmiş-zaman, hoca çakışması, müsaitlik penceresi** kuralları (kendi kaydı hariç) tekrar
  uygulanır; EndAt yeni başlangıçtan yeniden hesaplanır. `keepBookings=true` rezervasyonları taşır;
  `false` aktif rezervasyonları `CancelledByStudio` yapar (opsiyonel telafi kredisiyle) ve seansı boşaltır.
  İz: `RescheduledAt`/`RescheduleReason`.
- **Cancel** (güçlendirildi): gövdede `reason` + `grantMakeupCredit`. İptal → `Cancelled`, aktif
  rezervasyonlar `CancelledByStudio`, `BookedCount=0`; grant true ise rezervasyon sahiplerine +1 kredi
  (seans başına bir kez). Makeup rezervasyonlarında tüketilen kredi ayrıca iade edilir.
- **Toplu kapanış (StudioClosure):** tarih aralığındaki planlı seanslara iptal / iptal+kredi /
  ofsetle erteleme. Detay: [makeup-credits-and-postponements.md](makeup-credits-and-postponements.md).

## Yetki & tenant
Owner/StudioAdmin tüm hocalar için müsaitlik/seans yönetir; **Instructor yalnızca kendi** profilini
(`InstructorProfile.UserId == self`). Student bu yönetim uçlarına erişemez (403). Tüm kayıtlar
`StudioId == token.StudioId` ile yüklenir → başka stüdyo 404. Body'den gelen StudioId'ye güvenilmez.

## Endpoint özeti
| Endpoint | İşlev |
|---|---|
| `GET /api/studio/instructor-availabilities?instructorProfileId=` | müsaitlik listesi (Instructor → kendi) |
| `GET/POST /api/studio/instructors/{id}/availabilities` | hocanın müsaitlikleri / ekle |
| `PUT/DELETE /api/studio/instructor-availabilities/{id}` | güncelle / sil |
| `GET /api/studio/class-sessions?from&to&instructorProfileId&classTypeId&status` | seans listesi (max 90 gün, vars. bugün+30) |
| `GET /api/studio/class-sessions/{id}` | tekil |
| `POST /api/studio/class-sessions` | tek seans |
| `POST /api/studio/class-sessions/bulk-generate` | aralık içi slot üret |
| `PUT /api/studio/class-sessions/{id}` · `POST …/cancel` · `DELETE …/{id}` | güncelle / iptal / sil |
| `GET /api/instructors/me` | hocanın kendi profil id'si (kendi ders/müsaitlik yönetimi için) |

Manuel doğrulama: `scripts/sprint3-smoke.sh` (müsaitlik + EndAt + çakışma + müsaitlik validasyonu +
bulk + instructor scope + tenant + student engeli).
