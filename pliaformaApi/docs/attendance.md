# Yoklama ve Katılım Takibi (Sprint 6)

## Attendance nedir?
`Attendance`, bir rezervasyonun (ClassBooking) **katılım kaydıdır**: öğrencinin o ders seansına gelip
gelmediği. Durumlar: `Present` (geldi), `Absent` (gelmedi), `Excused` (mazeretli), `LateCancel` (geç iptal).
Tenant-scoped; ödeme/paket hakkı **DÜŞÜRMEZ** (yalnızca katılım bilgisidir).

## Attendance neden Booking üzerinden çalışır?
Yoklama, "kim derse yazıldı"nın (booking) üzerine kurulur: yalnızca bir seansa **rezervasyonu olan** öğrenci
için yoklama alınır. Booking olmayan öğrenci için yoklama yazılamaz. `Attendance.ClassBookingId` zorunludur
ve **booking başına en fazla bir** yoklama vardır (PostgreSQL filtered unique index `(ClassBookingId)
WHERE IsDeleted=false`). `ClassSessionId`/`StudentProfileId` booking ile tutarlı şekilde denormalize tutulur
(seans listesi + öğrenci geçmişi sorguları için).

## Attendance ile Booking farkı nedir?
- **Booking** = derse **yazılma/yer ayırtma** (arz tüketimi, kapasite).
- **Attendance** = derse **fiilen katılım** (geldi/gelmedi). Booking yoklamanın üzerine kurulduğu kayıttır;
  yoklama onu tüketmez, durumunu raporlar.

## Present / Absent / Excused / LateCancel anlamları
| Durum | Anlam | Kredi |
|---|---|---|
| Present | Geldi | Yok |
| Absent | Mazeretsiz gelmedi | Yok |
| Excused | Mazeretli gelmedi | **Opsiyonel** +1 (grant işaretlenirse) |
| LateCancel | Geç iptal | **Opsiyonel** +1 (grant işaretlenirse) |

## Mazeretli attendance krediye nasıl bağlanır?
Yalnızca **Excused** ve **LateCancel** durumlarında, işaretleyen `grantMakeupCredit=true` verirse öğrenciye
**+1 telafi kredisi** verilir (otomatik DEĞİL — manuel seçim). Reason: Excused → `ExcusedAbsence`,
LateCancel → `NoShowApproved`. Kredi hareketi `MakeupCreditTransaction` (Granted +1, `SourceClassSessionId`
+ `SourceBookingId` dolu) olarak deftere yazılır ve **yoklama ile aynı transaction'dadır**. Present/Absent
asla kredi vermez. Otomatik "her mazeretli gelmediye kredi" YOKTUR.

## Attendance kaynaklı kredi ikinci kez neden verilmez?
Yoklama kaydında `MakeupCreditTransactionId` alanı, o yoklamadan doğan krediyi işaret eder. Bir yoklama için
bu alan zaten doluysa, durum tekrar Excused/LateCancel + grant ile kaydedilse bile **ikinci kez kredi
verilmez**. Ayrıca `(StudentProfileId, SourceClassSessionId) WHERE Type='Granted'` filtered unique index'i
DB seviyesinde seans başına tek otomatik krediyi garanti eder.

## Status değişirse daha önce verilen krediye ne olur?
**Güvenli karar: otomatik geri alma YAPILMAZ.** Bir yoklama Excused+grant ile kredi ürettiyse ve sonradan
Present/Absent'a çevrilirse, daha önce verilen kredi **otomatik silinmez/iade edilmez**. Düzeltme gerekiyorsa
yönetici **manuel revoke** (`/makeup-credits/revoke`) kullanır ya da ters bir hareket eklenir. Bu, defter
bütünlüğünü (soft delete yok) ve öngörülebilirliği korur. `MakeupCreditTransactionId` yoklamada kalır, böylece
aynı yoklamadan tekrar kredi üretilmez.

## Gelecek session neden işaretlenemez?
Yoklama "ders yapıldı mı" sorusudur; bu yüzden yalnızca **başlamış/bitmiş** seanslarda alınabilir. Seansın
`StartAt`'i stüdyo-yerel "şimdi"den ilerideyse (gelecek) → **409**. İptal edilmiş (Cancelled) seans için de
yoklama alınamaz → 409. Bu sürümde seans otomatik `Completed` yapılmaz; yalnızca **zaman kontrolü** (StartAt
geçmiş) yeterlidir.

## Öğrenci kendi attendance geçmişini nasıl görür?
`GET /api/student/attendance` → öğrencinin **yalnızca kendi** kayıtları (ders adı, hoca, tarih, durum, kredi
verilmiş mi). `GET /api/student/attendance/summary?month=YYYY-MM` → aylık sayımlar (present/absent/excused/
lateCancel/total + attendanceRate). Öğrenci başkasının yoklamasını GÖREMEZ; staff yoklama uçlarına erişemez (403).

## Yetki & tenant özeti
- **Görüntüleme/işaretleme:** Owner/StudioAdmin tüm seanslar; **Instructor yalnızca kendi** InstructorProfile'ına
  bağlı seans (aksi 403). Student staff uçlarına erişemez (403).
- **Kredi verme (Excused/LateCancel):** Instructor ancak politika `AllowInstructorManualGrant=true` ise yoklama
  üzerinden kredi verebilir (kapalıysa 403); Owner/StudioAdmin her zaman.
- **Tenant:** tüm kayıtlar `StudioId == token.StudioId` ile yüklenir → başka stüdyo **404**. `ClassBooking`,
  `ClassSession`, `StudentProfile` aynı stüdyoya ait olmalıdır; body'den gelen StudioId'ye güvenilmez.
- **Staff öğrenci özeti** (`/api/studio/students/{id}/attendance/summary`): Instructor yalnızca erişebildiği
  öğrenci (kendi oluşturduğu / kendi seansında rezervasyonu olan).

## Attendance ileride dashboard ve ödeme/paket mantığına nasıl zemin hazırlar?
Bu sprint **basit aylık özet** verir (sayımlar + oran). İleride:
- **Dashboard (Sprint 8 — YAPILDI):** studio/instructor/student dashboard'ları bu aylık katılım sayımlarını
  (session StartAt'e göre) ve hoca PerStudentPerSession kazanç hesabını (Present sayısı) kullanır. Detay:
  [dashboards-and-reports.md](dashboards-and-reports.md).
- **Ödeme/paket (Sprint 7 — YAPILDI):** yoklama işaretlenirken `consumePackageCredit` ile öğrencinin
  paketinden **1 ders hakkı** atomik düşülür (düşüm + yoklama aynı transaction; uygun paket yoksa 409 +
  rollback). Yalnızca Owner/StudioAdmin; Instructor düşüremez. Detay: [packages-and-payments.md](packages-and-payments.md).
  Yoklama **ödeme** kaydını etkilemez (Payment ve Package ayrıdır).

## Bu sürümde bilinçli yapılmayanlar
Gelişmiş dashboard, ödeme/paket düşme, bildirim, recurring booking, **otomatik geç-iptal/no-show cezası**,
**otomatik kredi (her mazeretliye)**, kredi expiry job, resmi rapor/export. Seans otomatik `Completed`
yapılmaz (yalnızca zaman kontrolü).

## Endpoint özeti
| Endpoint | İşlev |
|---|---|
| `GET /api/studio/class-sessions/{id}/attendance` | seans yoklama listesi (Booked rezervasyonlar + canMark) |
| `PUT /api/studio/class-sessions/{id}/attendance` | batch işaretleme (`items[]`) |
| `PUT /api/studio/bookings/{id}/attendance` | tek booking işaretleme |
| `GET /api/studio/students/{id}/attendance/summary?month=` | staff: öğrenci aylık özeti |
| `GET /api/student/attendance` | öğrenci kendi geçmişi |
| `GET /api/student/attendance/summary?month=` | öğrenci kendi aylık özeti |

Manuel doğrulama: `scripts/sprint6-smoke.sh` (gelecek→409, geçmiş→200, batch upsert/no-duplicate,
Present/Absent kredisiz, Excused/LateCancel+grant +1 ve ikinci kez vermez, iptal booking/seans engeli, özet,
instructor scope, tenant 404, student 403). Not: "başlamış seans" gerektiğinden smoke, seansı ileri tarihle
açıp psql ile geçmişe çeker (create endpoint geçmiş seansı reddeder).
