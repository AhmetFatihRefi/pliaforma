# Ders Rezervasyonu ve Kapasite (Sprint 4)

## Booking nedir?
`ClassBooking`, bir öğrencinin bir `ClassSession`'a **rezervasyonudur**. **`StudentProfileId` üzerinden**
çalışır (UserId değil) — böylece misafir öğrenci de (personel aracılığıyla) rezerve edilebilir. Aktif
rezervasyon yalnızca `Status=Booked`'dır. Tenant-scoped.

## Booking ile Attendance (yoklama) farkı nedir?
Booking = **derse yazılma niyeti/yeri** (arz tüketimi). Attendance (yoklama) = öğrencinin derse fiilen
**gelip gelmediği**. Bu sprintte yoklama YOKTUR (Sprint 5). Booking, yoklamanın üzerine kurulacağı kayıttır.

## Booking ile Payment/Package farkı nedir?
Booking **ödeme/paket kontrolü yapmaz** — paket hakkı olmasa da rezervasyon yapılabilir. **Sprint 7 (yapıldı):**
paket hakkı düşme rezervasyona değil **yoklamaya** (Attendance) bağlandı: yoklama işaretlenirken staff
`consumePackageCredit` ile paketten 1 hak düşürür. Ödeme (Payment) ise hak düşürmez (ayrı kavram). Detay:
[packages-and-payments.md](packages-and-payments.md).

## Guest booking kuralı nedir?
- **Staff (Owner/StudioAdmin/Instructor)** bir **Guest** öğrenci adına rezervasyon yapabilir (telefonla
  gelen misafir derse yazılabilir).
- **Self booking misafir yapamaz:** misafirin User hesabı yoktur (giriş yapamaz), dolayısıyla yapısal
  olarak self-booking mümkün değildir.
- **Inactive** öğrenci için (staff veya self) rezervasyon yapılamaz → 409.

## Student self booking kuralı nedir?
Öğrenci yalnızca aktif (Status=Active) **kendi** `StudentProfile`'ı ile rezervasyon yapar
(`POST /api/student/class-sessions/{id}/book`). StudioId token'dan; profil UserId eşleşmesiyle bulunur.

## Kapasite nasıl korunuyor?
`ClassSession.BookedCount` alanı aktif rezervasyon sayısını tutar. Rezervasyonda **atomik koşullu UPDATE**
ile artırılır: `UPDATE ... SET BookedCount = BookedCount + 1 WHERE BookedCount < Capacity`. Etkilenen satır
0 ise seans dolu → 409. İptalde `BookedCount = GREATEST(BookedCount - 1, 0)` ile azaltılır.

## Race condition nasıl engelleniyor?
İki katman:
1. **Atomik koşullu UPDATE** (satır kilidi): UPDATE sırasında PostgreSQL ilgili satıra kilit alır ve bu
   kilit aktif transaction commit edilene kadar tutulur. Eşzamanlı iki rezervasyon: ikincinin UPDATE'i
   birincinin commit'ini bekler, sonra güncel `BookedCount`'u görür; doluysa `BookedCount < Capacity`
   koşulu başarısız → 0 satır → 409. Sadece `COUNT + INSERT` KULLANILMAZ (yarışta patlar).
2. **Filtered (partial) unique index** (çift-rezervasyon): `(ClassSessionId, StudentProfileId)` üzerinde
   `WHERE Status='Booked' AND IsDeleted=false`. Aynı öğrencinin aynı seansta iki aktif kaydı DB seviyesinde
   engellenir.

Mimari sınır: atomik SQL **Persistence**'tadır (`EfClassSessionCapacityService`); Application yalnızca
`IClassSessionCapacityService` soyutlamasını görür (Npgsql/raw SQL sızmaz). Kapasite tüketimi + booking
insert aynı transaction'da (TransactionBehavior); herhangi bir hata **rollback** ile koltuk artışını da
geri alır.
> **Doğrulandı (smoke):** Capacity=1 seansa 5 eşzamanlı rezervasyon → **tam 1 başarılı, 4 × 409**.

## Cancel sonrası kapasite nasıl açılıyor?
İptal: `Status` → `CancelledByStudent`/`CancelledByStudio` (iptal edene göre), `CancelledByUserId/At`
set edilir ve `ReleaseSeatAsync` ile `BookedCount` atomik azaltılır — hepsi tek transaction'da. İptal
edilen kayıt **silinmez** (status değişir). Sonraki rezervasyon açılan koltuğu kullanabilir.

## Aynı öğrenci tekrar booking yapabilir mi?
- Aktif (Booked) bir rezervasyonu varken **aynı seansa ikinci aktif** rezervasyon yapamaz → 409.
- Rezervasyonu **iptal edilmişse**, iptal edilen kayıt aktif sayılmaz; öğrenci aynı seansa **tekrar**
  rezervasyon yapabilir (filtered index yalnızca Booked'ları kısıtlar). Doğrulandı (smoke).

## Session cancel edilirse booking'ler ne olur?
Bir `ClassSession` iptal edilince (CancelClassSession), o seansın aktif rezervasyonları **CancelledByStudio**
yapılır ve `BookedCount = 0` set edilir (cascade). Bu davranış bilinçli eklendi. **Sprint 5:** iptal artık
opsiyonel `reason` + `grantMakeupCredit` alır; grant true ise sahiplerine +1 telafi kredisi verilir
(seans başına bir kez), ayrıca makeup rezervasyonlarında tüketilmiş kredi iade edilir.

## Makeup seansı booking'i kredi tüketir mi? (Sprint 5)
`ClassSession.SessionKind == Makeup` ise `BookingCreator` 1 telafi kredisi tüketir: öğrenci satırını
`FOR UPDATE` ile kilitler, bakiye > 0 ise `Used` (−1) hareketi ekler — **booking insert ile aynı
transaction'da** (rollback ikisini de geri alır). Kredi yoksa **409**. Makeup rezervasyonu iptal edilirse
kredi **Restored** (+1) ile iade edilir. Regular seans booking'i krediyi etkilemez. Detay:
[makeup-credits-and-postponements.md](makeup-credits-and-postponements.md).

## Yetki & tenant
- Self: Student kendi aktif profili.
- Staff: Owner/StudioAdmin herhangi bir Active/Guest öğrenci; **Instructor yalnızca kendi seansı + kendi
  oluşturduğu öğrenci**.
- Cancel: Student kendi rezervasyonu; Owner/StudioAdmin herhangi biri; Instructor kendi seansındakiler.
- Tüm kayıtlar `StudioId == token.StudioId` ile yüklenir → başka stüdyo **404**. Body'den gelen StudioId'ye
  güvenilmez. Smoke: Owner C, A'nın seansına booking/booking get/cancel → 404.

## Bildirim zemini (Sprint 9)
Booked rezervasyonlar **ders hatırlatma** (ClassReminder) üretiminin kaynağıdır: dersten ~1 gün önce, Scheduled
seans + Booked booking + öğrenci User'lı ise tek bildirim. İptal booking / iptal seans / misafir (User'sız) için
üretilmez. Bildirim rezervasyon iş kuralını değiştirmez. Detay: [notifications-and-reminders.md](notifications-and-reminders.md).

## Sonraki sprintler için zemin
**Sprint 5 (yapıldı):** telafi (makeup) kredisi + makeup seans booking'i + erteleme/toplu kapanış booking
üzerine eklendi. **Sprint 6 (yapıldı):** **Yoklama (Attendance)** her Booked rezervasyon üzerine kuruldu
(geldi/gelmedi/mazeretli/geç-iptal; yalnızca aktif Booked rezervasyonlar yoklama listesine gelir; iptal
rezervasyon/iptal seans → yoklama alınmaz). Detay: [attendance.md](attendance.md). Bekleme listesi, geç-iptal
**politikası** (otomatik), ders hatırlatma hâlâ YOK; sonraki sprintlerde gelir.

## Endpoint özeti
| Endpoint | İşlev |
|---|---|
| `GET /api/studio/bookings?classSessionId&status` | stüdyo rezervasyon listesi (Instructor → kendi seansları) |
| `GET /api/studio/bookings/{id}` | tekil |
| `GET /api/studio/class-sessions/{sessionId}/bookings` | bir seansın rezervasyonları |
| `POST /api/studio/class-sessions/{sessionId}/bookings` | vekaleten rezervasyon (staff) |
| `POST /api/studio/bookings/{id}/cancel` | staff iptal |
| `GET /api/student/bookings` | öğrencinin yaklaşan rezervasyonları |
| `GET /api/student/class-sessions?from&to&classTypeId` | rezerve edilebilir seanslar (doluluk + alreadyBooked) |
| `POST /api/student/class-sessions/{sessionId}/book` | self rezervasyon |
| `POST /api/student/bookings/{id}/cancel` | self iptal |

ClassSession DTO'suna eklendi: `bookedCount`, `remainingCapacity`, `isFull`. Manuel doğrulama:
`scripts/sprint4-smoke.sh` (kapasite + çift-booking + iptal/geri-açma + **paralel yarış** + guest/inactive/
self + instructor scope + tenant).
