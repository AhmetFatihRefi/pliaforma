# Dashboard ve Temel Raporlar (Sprint 8)

Mevcut verileri rol bazlı, **read-only** özetlere dönüştüren sprint. Yeni iş kuralı/şema yoktur (migration
yok). Sorgular `AsNoTracking` + projection; aralık her zaman **tek ay** (geniş aralık riski yok).

## Studio dashboard hangi verileri gösterir?
`GET /api/studio/dashboard?month=YYYY-MM` (Owner/StudioAdmin): öğrenci sayıları (toplam/aktif/misafir/bu ay
yeni), hoca sayısı, bu ay seans (toplam/yapılmış/iptal), bu ay rezervasyon, **katılım dökümü** (present/absent/
excused/lateCancel + oran), **ödemeler** (paid/pending/overdue tutar+adet — DueDate bu ay; Overdue dinamik),
**paketler** (aktif paket sayısı — effectiveStatus, kullanılan kredi, kalan kredi toplamı), **telafi
kredileri** (toplam açık bakiye, bu ay verilen/kullanılan), **yaklaşan** (bugünkü dersler, gelecek 7 gün).

## Instructor dashboard earning hesabı nasıl yapılır?
`GET /api/instructor/dashboard?month=` (hoca kendi) / `GET /api/studio/instructors/{id}/dashboard` (Owner/Admin).
Kazanç **TAHMİNİDİR** (ödemeyle bire bir eşleşmez; gelir seansa bağlı tutulmuyor). Yalnızca o ay **yapılmış**
(Cancelled değil, StartAt geçmiş) seanslara göre, `InstructorProfile.RateType`'a bağlı:
- **PerSession:** yapılmış ders sayısı × rate.
- **PerHour:** yapılmış seansların toplam saati × rate (süre = EndAt − StartAt).
- **PerStudentPerSession:** o ay Present yoklama sayısı × rate.

`earningCalculationNote` alanı kullanılan formülü ve "tahmini" uyarısını taşır. Ayrıca: sessions/cancelled/
bookings, present/absent/excused, toplam öğretilen öğrenci (distinct), ClassType dağılımı.

## Percentage/gelir-paylaşımı rate neden tam hesaplanmıyor?
Mevcut `RateType` kataloğu `PerSession | PerHour | PerStudentPerSession`'dır; **yüzde/gelir-paylaşımı tipi
yoktur**. Böyle bir tip eklenirse gelir, ödemelerin seanslara dağıtımını gerektirir (Payment şu an seansa
bağlanmıyor). Bu yüzden bilinmeyen/yüzde tipinde `estimatedEarning = null` döner ve not alanında "gelir
paylaşımı/yüzde tipi ayrı sprintte hesaplanacak" yazar. Mevcut tipler için kazanç concrete hesaplanır.

## Student dashboard hangi özetleri verir?
`GET /api/student/dashboard?month=` (öğrenci kendi): yaklaşan rezervasyonlar, aylık katılım özeti, paket özeti
(aktif paket sayısı, kalan kredi, en yakın paket bitişi), ödeme özeti (bekleyen/gecikmiş tutar, en yakın vade),
telafi kredisi bakiyesi, son telafi hareketleri / son yoklamalar / son rezervasyonlar. Paketi/ödemesi yoksa
0/null ile **boş durum** döner (endpoint patlamaz). Yalnızca User'a bağlı **Active** StudentProfile için çalışır
(misafirin self dashboard'u yoktur).

## Month filter nasıl çalışır?
`month=YYYY-MM` verilirse o ay; geçersiz/boşsa **stüdyo-yerel içinde bulunulan ay**. Aralık her zaman tek aydır
(`ReportPeriod`). Üç sınır türü kullanılır: **local (duvar saati)** ClassSession.StartAt için; **UTC** audit
(CreatedAt/PaidAt) için; **DateOnly** tarih (Payment.DueDate, StudentPackage.ValidUntil) için. Katılım, ayın
**session StartAt**'ine göre filtrelenir (Sprint 6 ile tutarlı). Bu yaklaşım mevcut projedeki timezone mantığıyla
uyumludur.

## Finansal veriler Instructor'dan neden gizlenir?
Paket fiyatları ve ödeme tutarları finansal/ticari bilgidir. Bu yüzden: studio dashboard + ödeme raporu +
hoca-bazlı rapor **Owner/StudioAdmin**'e açıktır. Instructor yalnızca **kendi** dashboard'unu (iş hacmi +
**kendi** estimated earning) görür; başka hocanınkini göremez (by-id endpoint 403). Bir öğrencinin staff
dashboard'unu Instructor çağırırsa **`paymentSummary` dahil edilmez** (null → API serileştirmede atlanır);
Owner/StudioAdmin çağırırsa finansal özet gelir.

## Overdue nasıl dinamik hesaplanır?
Kalıcı `Overdue` durumu tutulmaz (job yok). Depodaki Status `Pending` **ve** `PaidAt` null **ve** `DueDate`
stüdyo-yerel **bugünden** önceyse → **effective Overdue**. Dashboard/rapor tutarları bu effective duruma göre
toplanır (`PaymentMapping.Effective`). Aynı şekilde paket `effectiveStatus` (Active ama ValidUntil geçmiş →
Expired) dashboard sayımlarında dikkate alınır.

## Bu sprintte neden chart/export yok?
Kapsam **read-only özet/temel rapor**tur. Ağır grafik kütüphanesi (zorunlu bağımlılık + bundle yükü),
Excel/PDF export (3. parti + biçimlendirme yükü), gelişmiş finans/muhasebe raporu ve veri ambarı/materialized
view **bilinçli olarak hariç**. UI'da basit kartlar + küçük CSS oranı çubuğu + tablolar kullanıldı.

## Bildirimlerle ilişkisi (Sprint 9)
Dashboard'lar **read-only**dir ve bildirim üretmez. Aynı kaynak veriler (yaklaşan ders, vadesi yaklaşan/geçen
ödeme) **bildirim hatırlatmalarının** da temelidir; bildirim üretimi ayrı bir akıştır (worker + manuel trigger).
Detay: [notifications-and-reminders.md](notifications-and-reminders.md).

## Platform overview ile farkı (Sprint 10)
Bu dashboard'lar **tek stüdyo** (tenant-scoped, `StudioId == token`) içindir. Platform admin'in
**cross-tenant** özeti (tüm stüdyolar: toplam/aktif/askıda stüdyo, kullanıcı/öğrenci/hoca, gecikmiş ödeme,
bildirim sağlığı) ayrı bir akıştır (`/api/platform/overview`, yalnızca PlatformAdmin) ve burada anlatılan
stüdyo dashboard'larından **bağımsızdır**. Detay:
[platform-admin-and-observability.md](platform-admin-and-observability.md).

## Dashboard ileride nasıl optimize edilir?
Şu an birkaç **basit ayrı sorgu** + in-memory toplama kullanılır (küçük/orta stüdyo için yeterli). Büyük
veride: (1) sayımları SQL `GROUP BY`/`COUNT` ile veritabanına itmek (in-memory liste yüklemeyi azaltır),
(2) sık erişilen aylık özetler için cache/önceden-hesaplanmış tablo, (3) gerekirse materialized view +
periyodik yenileme. Bu sprintte kasıtlı olarak en sade yol seçildi (rule: ağır background job / veri ambarı yok).

## Yetki & tenant özeti
| Endpoint | Erişim |
|---|---|
| `GET /api/studio/dashboard` | Owner/StudioAdmin |
| `GET /api/studio/instructors/{id}/dashboard` | Owner/StudioAdmin |
| `GET /api/studio/students/{id}/dashboard` | Owner/StudioAdmin (tüm) · Instructor (erişebildiği öğrenci, finans gizli) |
| `GET /api/studio/reports/payments` · `/reports/instructors` | Owner/StudioAdmin |
| `GET /api/instructor/dashboard` | Instructor (kendi) |
| `GET /api/student/dashboard` | Student (kendi) |

Tüm sorgular `StudioId == token.StudioId` ile sınırlıdır → başka stüdyo **404** (by-id) ve veriye sızma yok
(smoke ile kanıtlı). Instructor başka hocanın iş hacmini göremez; Student yalnızca kendi verisini görür.

Manuel doğrulama: `scripts/sprint8-smoke.sh` (studio sayımları, PerSession/PerStudent earning, dinamik Overdue,
student özetleri, month ayrımı, boş veri patlamaz, instructor finans gizli, rol kapıları, tenant 404). Not:
attendance/earning verisi için seanslar ileri açılıp psql ile 2020-01'e çekilir; dashboard'lar `month=2020-01`
ile okur.
