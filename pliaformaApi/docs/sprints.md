# Sprint Yol Haritası

Her sprint **backend dilim + eşleşen frontend dilim** olarak ilerler; bir taraftaki sözleşme
değişikliği diğer tarafta aynı anda güncellenir.

| Sprint | Backend | Frontend |
|---|---|---|
| **0 ✅** | İskelet + Auth/Studio/Membership (register-studio/register/login/switch/refresh/logout/me) | Shell + tema + component sistemi + auth/studio sayfaları + dashboard iskeleti |
| **1 ✅** | Hoca daveti/başvurusu (membership uçları), InstructorProfile+ücret, ClassType taksonomisi, hoca↔grup + **ilk IStudioWriteRequest guard'ları + tenant izolasyon testi** | Stüdyo ayarları, hoca & grup yönetimi (class-types/instructors/settings) |
| **2 ✅** | StudentProfile + misafir akışı + User'a dönüştürme (Id korunur) + hoca scope + Student self + tenant testleri | Öğrenci listesi/arama, misafir formu, dönüştürme modalı, profilim, rol-duyarlı sidebar |
| **3 ✅** | InstructorAvailability + ClassSession (tek + bulk slot, EndAt otomatik, çakışma & müsaitlik & geçmiş kuralları) + instructor scope + tenant testleri | Ders Programı + Müsaitlik ekranları |
| **4 ✅** | ClassBooking (atomik kapasite, çift-rezervasyon, vekaleten + self), iptal/geri-açma, doluluk, paralel yarış testi | Schedule doluluk+booking, Ders Bul, Rezervasyonlarım |
| **5 ✅** | Ders erteleme/iptal güçlendirme, toplu kapanış (StudioClosure), telafi kredisi ledger (MakeupCreditTransaction), makeup seans booking kredi tüketimi, manuel grant/revoke + politika | Schedule reschedule/cancel modalları, Kapalı Günler, Telafi Kredileri, Telafi Kredilerim |
| **6 ✅** | Yoklama (Attendance, booking üzerine): batch/single işaretleme, Excused/LateCancel opsiyonel telafi kredisi, aylık özet, zaman/rol/tenant kuralları | Schedule yoklama modalı, Katılım Geçmişim, öğrenci özet kartı |
| **7 ✅** | Öğrenci paketleri + ödeme takibi: PackagePlan, StudentPackage (+ledger), Payment (manuel), Attendance→paket hakkı düşürme (atomik) | Paket Planları, Ödemeler, öğrenci paket/ödeme modalı, Paketlerim/Ödemelerim |
| **8 ✅** | Dashboard ve temel raporlar (read-only): studio/instructor/student dashboard, ödeme & hoca iş hacmi raporu, rol bazlı + finans gizliliği | Rol bazlı /dashboard, /dashboard/reports |
| **9 ✅** | Bildirim & hatırlatmalar: Notification + idempotent reminder scanner + worker, ders/ödeme hatırlatma, in-app okuma/unread, kanal soyutlaması (InApp/Log; SMS/WhatsApp no-op) | Notification bell, /dashboard/notifications, manuel trigger |
| **10 ✅** | Platform admin (cross-tenant), studio suspend/reactivate (StudioStatus), PlatformSubscription iskeleti, overview/health/recent-activity, indeks/performans incelemesi | `/platform` paneli (overview/studios/detail/health) |
| **11 ✅** | Pilot hazırlığı/QA: gerçek tarayıcı E2E (Playwright), e2e-seed, prod config/env audit, regresyon, pilot+test docs (yeni domain feature YOK) | E2E test altyapısı, auth-redirect polish, mobil/console denetimi |
| **12 ✅** | MVP freeze/güvenlik denetimi: repo+secret hygiene, security review, security-audit-smoke, backup/restore (test edildi), nginx secure headers, validation runner, release docs (feature YOK) | (UI değişmedi; E2E 25/25 re-confirm) |

## Sprint 0 — Tamamlanan kapsam
**Yapıldı:** 5 katman + CQRS/behaviors, User/Studio/Membership/RefreshToken, auth dikey kesiti,
PostgreSQL + migration, health, rate limit, env güvenliği (fail-fast secret), seed admin, frontend shell.
**Yapılmadı (sonraki sprintler):** rezervasyon, ödeme, yoklama, bildirim worker'ı, hoca takvimi,
recurring booking, dashboard hesaplamaları, hoca/öğrenci profil CRUD'ları.

## Sprint 1 — Tamamlanan kapsam
**Yapıldı:** Üyelik akışı (invite/apply/accept/reject/remove/list/my-invitations), InstructorProfile
(ücret, stüdyo bazında), ClassType taksonomisi + CRUD (kapasite validasyonu), InstructorClassType
atama, stüdyo ayarları (get/update). İlk `IStudioWriteRequest` guard'ları + rol kapıları
(`Owner/StudioAdmin`) + **tenant izolasyon ve rol testleri geçti** (`scripts/sprint1-smoke.sh`).
Frontend: class-types, instructors (davet/ücret/atama/çıkar), settings sayfaları + sidebar.
**Yapılmadı:** StudentProfile/misafir akışı (Sprint 2), e-posta tabanlı dış davet, StudioAdmin daveti.

## Sprint 2 — Tamamlanan kapsam
**Yapıldı:** `StudentProfile` (tenant-scoped, soft-delete/inactive), misafir oluşturma (User'sız),
misafir→User dönüşümü (mevcut/yeni User, **Id korunur**), güncelle/pasifleştir/aktifleştir, liste+arama,
hoca scope (yalnızca kendi oluşturduğu), Student self (`/api/student/me`), HealthNotes maskeleme.
Frontend: `/dashboard/students` (liste/arama/oluştur/düzenle/dönüştür), `/dashboard/my-profile`,
rol-duyarlı sidebar. Tenant izolasyonu + instructor scope + student engeli + Id korunumu smoke ile
kanıtlı (`scripts/sprint2-smoke.sh`, 25/25). Ek: paket sürümleri kurulu runtime 10.0.2 ile hizalandı
(FileNotFound fix), auth rate limit yapılandırılabilir (Dev'de gevşek).
**Yapılmadı (Sprint 2 dışı):** rezervasyon, ödeme, paket, yoklama, ders takvimi, bildirim.

## Sprint 3 — Tamamlanan kapsam
**Yapıldı:** `InstructorAvailability` (haftalık pencere, çakışma engeli), `ClassSession` (tek + bulk
slot), **EndAt = StartAt + ClassType.DefaultDurationMinutes**, hoca çakışma kontrolü, müsaitlik
penceresi validasyonu (availability yoksa Owner/Admin serbest, Instructor reddedilir), geçmiş-zaman
engeli, capacity vars. MaxCapacity, listede tarih aralığı (max 90 gün) + filtreler, iptal/sil,
`/api/instructors/me`. Instructor yalnızca kendi; Student erişemez; tenant izolasyonu. smoke
`scripts/sprint3-smoke.sh` 24/24. Frontend: `/dashboard/schedule` (filtre + tek/bulk modal + iptal),
`/dashboard/availability`, sidebar. Timezone: stüdyo-yerel duvar saati (UTC dönüşümü ertelendi, docs'ta).
**Yapılmadı (Sprint 3 dışı):** öğrenci rezervasyonu, kapasite tüketimi, yoklama, ödeme, recurring booking,
telafi, bildirim.

## Sprint 4 — Tamamlanan kapsam
**Yapıldı:** `ClassBooking` (StudentProfileId üzerinden), self + vekaleten (staff) rezervasyon, **atomik
kapasite** (BookedCount + koşullu UPDATE, race-safe), **filtered unique index** ile çift-rezervasyon
koruması, iptal + kapasite geri açma, session-cancel cascade, doluluk alanları (bookedCount/remaining/
isFull), öğrenci self ekranları. Yetki: self yalnızca aktif kendi profili; staff (Owner/Admin tümü,
Instructor kendi seansı+kendi öğrencisi); Guest staff-booking OK, self değil; Inactive yapamaz. smoke
`scripts/sprint4-smoke.sh` (paralel yarış dahil: cap=1'e 5 eşzamanlı → tam 1 başarılı). Frontend:
schedule doluluk+staff booking modal+rezervasyon listesi, `/dashboard/book-classes`, `/dashboard/my-bookings`,
rol-duyarlı sidebar. Mimari: atomik SQL Persistence'ta (`IClassSessionCapacityService`).
**Yapılmadı (Sprint 4 dışı):** yoklama, ödeme, paket, telafi, bildirim, recurring, bekleme listesi, geç-iptal.

## Sprint 5 — Tamamlanan kapsam
**Yapıldı:** `ClassSession.SessionKind` (Regular/Makeup/Trial/Special); tekil **erteleme**
(`reschedule`, keepBookings + Sprint 3 zaman/çakışma/müsaitlik kuralları) ve güçlendirilmiş **iptal**
(reason + `grantMakeupCredit`); **toplu kapanış** `StudioClosure` (CancelSessions / CancelAndGrantMakeupCredit /
RescheduleByOffset; affected/cancelled/credited/rescheduled/skipped sonucu); **telafi kredisi defteri**
`MakeupCreditTransaction` (Granted/Used/Restored/Revoked/Expired; bakiye = SUM(Amount)); **makeup seans
booking** 1 kredi tüketir (booking + kredi aynı transaction; kredisiz → 409); makeup iptalinde kredi
**Restored**; manuel grant/revoke (+ aylık limit, instructor erişim/politika kuralı); `StudioMakeupPolicy`
(get/update). Concurrency: kredi tüketiminde öğrenci satırı `FOR UPDATE` kilidi (`IMakeupCreditLock`);
çift kredi/çift tüketim **filtered unique index**'lerle DB seviyesinde engellenir. smoke
`scripts/sprint5-smoke.sh` (39 kontrol) + same-origin (:3000) geçti; 6 önceki smoke regresyon yeşil.
Frontend: schedule SessionKind rozeti + reschedule/cancel modalları, `/dashboard/closures`,
`/dashboard/makeup-credits` (+ politika), `/dashboard/my-makeup-credits`, book-classes makeup gating, sidebar.
**Bilinçli yapılmadı:** kredi ExpiresAt zorlaması/otomatik expiry, geç-iptal politikası, yoklama, ödeme,
paket düşme, bildirim worker, recurring booking, bekleme listesi, otomatik resmi tatil entegrasyonu.
Detay: [makeup-credits-and-postponements.md](makeup-credits-and-postponements.md).

## Sprint 6 — Tamamlanan kapsam
**Yapıldı:** `Attendance` (TS, **ClassBooking üzerinden**, booking başına tek kayıt — filtered unique index),
`AttendanceStatus` (Present/Absent/Excused/LateCancel). Seans bazlı yoklama listesi (yalnız Booked
rezervasyonlar + `canMark`), **batch + single işaretleme** (upsert; ikinci işaretleme update, duplicate yok).
Zaman kuralı: yalnızca başlamış/bitmiş seans (StartAt geçmiş) — gelecek seans 409, iptal seans 409, iptal
booking 409. **Excused/LateCancel + grantMakeupCredit** → +1 telafi kredisi (manuel; Present/Absent kredisiz;
aynı yoklamadan ikinci kez verilmez; reason ExcusedAbsence/NoShowApproved; kredi yoklama ile aynı transaction).
Status değişince önceki kredi **otomatik geri alınmaz** (manuel revoke). Basit aylık özet (staff + student self).
Yetki: Owner/Admin tümü, Instructor kendi seansı (kredi için politika), Student yalnızca kendi geçmişi.
Migration `Sprint6_Attendance`. smoke `scripts/sprint6-smoke.sh` (28 kontrol) + same-origin (:3000) + 7 önceki
smoke regresyon + **compose dev up --build** (tam stack) hepsi yeşil. Frontend: schedule yoklama modalı,
`/dashboard/my-attendance`, öğrenci özet kartı (students), sidebar. **Bilinçli yapılmadı:** ödeme/paket düşme,
gelişmiş dashboard, bildirim, recurring, otomatik geç-iptal/no-show cezası, otomatik kredi, kredi expiry job,
rapor/export, otomatik Completed. Detay: [attendance.md](attendance.md).

## Sprint 7 — Tamamlanan kapsam
**Yapıldı:** `PackagePlan` (paket şablonu CRUD), `StudentPackage` (öğrenciye satılan paket; **ledger ile
senkron** `CurrentCreditBalance`; Active/UsedUp/Expired/Cancelled), `StudentPackageTransaction` (Granted/Used/
Restored/Adjusted/Revoked defteri), `Payment` (manuel ödeme; Pending/Paid/Overdue/Cancelled/Refunded; **Overdue
DİNAMİK**). **Attendance → paket hakkı düşürme:** yoklamada `consumePackageCredit` (+ opsiyonel `studentPackageId`)
ile **atomik** −1 (race-safe `IStudentPackageCreditService`, bakiye negatife düşmez, 0'da UsedUp); düşüm +
yoklama aynı transaction → uygun paket yoksa **409 + rollback** (yoklama da kaydedilmez). **Aynı yoklama için
tek düşüm** (filtered unique index `(SourceAttendanceId) WHERE Type='Used'`). Status değişince düşülen hak
otomatik geri alınmaz (manuel adjust). **Payment ile Package ayrı** (ödeme hak düşürmez). Yetki: paket/ödeme
yönetimi **Owner/StudioAdmin** (Instructor finans/tutar görmez, paket düşüremez → 403); öğrenci yalnızca kendi.
Migration `Sprint7_PackagesAndPayments`. smoke `scripts/sprint7-smoke.sh` (38 kontrol) + same-origin (:3000) +
8 önceki smoke regresyon + **compose dev up --build** (tam stack) yeşil. Frontend: `/dashboard/package-plans`,
`/dashboard/payments`, students "Paket/Ödeme" modalı, schedule yoklama modalına paket düşüm, `/dashboard/
my-packages`, `/dashboard/my-payments`, sidebar. **Bilinçli yapılmadı:** online ödeme gateway, banka/iyzico/PayTR,
otomatik tahsilat, fatura/e-arşiv, muhasebe, kredi kartı saklama, paket dondurma, otomatik package expiry job,
kampanya/kupon, bildirim, gelişmiş finans raporu. Detay: [packages-and-payments.md](packages-and-payments.md).
**Sprint 7 Fix:** paket tüketiminde `ValidUntil` zorlanır (expired paket kullanılamaz; auto-select atlar) +
DTO'da dinamik `effectiveStatus` (DB Active ama süresi geçmiş → Expired). Background expiry job hâlâ yok.

## Sprint 8 — Tamamlanan kapsam
**Yapıldı (read-only, migration YOK):** rol bazlı dashboard'lar — **Studio** (`/api/studio/dashboard`,
Owner/Admin: öğrenci/seans/rezervasyon/katılım/ödeme/paket/telafi/yaklaşan özetleri), **Instructor**
(`/api/instructor/dashboard` + `/api/studio/instructors/{id}/dashboard`: iş hacmi + **tahmini kazanç** —
PerSession/PerHour/PerStudentPerSession), **Student** (`/api/student/dashboard`: yaklaşan/katılım/paket/ödeme/
telafi özetleri), **staff öğrenci dashboard** (`/api/studio/students/{id}/dashboard`; Instructor erişebildiği
öğrenci, **finans gizli**). **Raporlar:** `/api/studio/reports/payments` (DueDate ay + dinamik Overdue) +
`/reports/instructors` (hoca bazlı, N+1 yerine grup sorgu). `AsNoTracking`+projection; tek ay aralığı;
`ReportPeriod` (local/utc/date sınırları). Overdue/effectiveStatus dinamik kullanılır. smoke
`scripts/sprint8-smoke.sh` (35 kontrol) + same-origin (:3000) + 9 önceki smoke regresyon + **compose dev up
--build** (tam stack) yeşil. Frontend: rol bazlı `/dashboard` (kart + CSS oran çubuğu), `/dashboard/reports`,
sidebar. **Bilinçli yapılmadı:** bildirim worker, online ödeme, Excel/PDF export, ağır grafik kütüphanesi,
gelişmiş finans/muhasebe raporu, recurring, package expiry job, ağır background job, materialized view.
Detay: [dashboards-and-reports.md](dashboards-and-reports.md).

## Sprint 9 — Tamamlanan kapsam
**Yapıldı:** `Notification` (TS + kullanıcı-scoped; Type/Channel/Status; **DeduplicationKey** stüdyo-içi unique).
**Idempotent `ReminderScanner`** (worker + manuel trigger paylaşır): ders hatırlatma (StartAt ~25h pencere,
Booked+Scheduled+UserId; `class-reminder:{bookingId}`), ödeme **DueSoon** (Pending+bugün/yarın) ve **Overdue**
(effective; Pending+geçmiş). Guest (UserId yok) → bildirim üretilmez. **Kanal soyutlaması**
`INotificationChannelSender` → `LoggingNotificationSender` (InApp/Log = log+Sent; SMS/WhatsApp/Email = **no-op**,
gerçek gönderim yok). **Worker** `ReminderBackgroundService` (interval config; aktif stüdyoları dolaşır; bir
stüdyo hata verirse loglar+devam; `WorkerEnabled` ile kapatılır — Dev'de kapalı, manuel trigger ile test).
Manuel trigger `POST /api/studio/notifications/run-reminders` (Owner/Admin). In-app: `GET /api/notifications`,
`/unread-count`, `POST {id}/read`, `/read-all` (yalnızca kendi; başkasınınki 404). Migration `Sprint9_Notifications`
(filtered unique dedup index). smoke `scripts/sprint9-smoke.sh` (21 kontrol) + same-origin + 10 önceki smoke
regresyon + **compose dev up --build** (tam stack) yeşil. Frontend: header notification bell (unread badge +
dropdown), `/dashboard/notifications` (+manager "çalıştır"), sidebar (tüm roller). Bildirimler booking/payment
iş kurallarını DEĞİŞTİRMEZ; teslim hatası akışı bozmaz. **Bilinçli yapılmadı:** gerçek SMS/WhatsApp/e-posta/push,
kullanıcı preferences, platform admin, online ödeme, export, kampanya. Detay: [notifications-and-reminders.md](notifications-and-reminders.md).

## Sprint 10 — Tamamlanan kapsam
**Yapıldı:** **Platform admin** (cross-tenant, yalnızca `GlobalRole.PlatformAdmin`). `Studio.IsActive` →
**`StudioStatus`** enum'a genişletildi (`Active/Suspended/Cancelled` + `SuspendedAt/Reason/ReactivatedAt/
UpdatedByPlatformAdminUserId`; `IsActive` hesaplanan property, kolon kaldırıldı, migration backfill'li).
**`PlatformSubscription`** basit takip iskeleti (Trialing/Active/PastDue/Suspended/Cancelled · Monthly/Yearly ·
stüdyo başına bir abonelik filtered unique index; **gerçek tahsilat yok**). Uçlar (`/api/platform/*`,
`[Authorize(Policy="PlatformAdmin")]` + handler `EnsurePlatformAdmin`): overview, studios (arama+durum filtresi,
GroupBy ile N+1'siz sayım), studio detail (sahip+abonelik+sayım+son aktivite), **suspend/reactivate** (veri
silinmez, gerekçe korunur), subscription **upsert/get/cancel**, **health-summary** (read-only) + recent-activity.
`StudioStatusBehavior` artık `Status != Active` ise yazmayı 403 ile engeller; **worker yalnızca Active stüdyoları
tarar** (askıdaki stüdyoya reminder üretilmez). **PlatformAdmin tenant uçlarını otomatik bypass etmez** (studio
bağlamı yok → 403). **Index incelemesi:** tek eksik **`Payments (StudioId, DueDate)`** eklendi. Migration
`Sprint10_PlatformAdmin`. smoke `scripts/sprint10-smoke.sh` (31 kontrol) + same-origin (:3000) + **11 önceki smoke
regresyon** + **compose dev up --build** (tam stack) yeşil. Frontend: ayrı `/platform` route grubu (kendi
layout/sidebar, stüdyo seçimi gerektirmez) — overview/studios/detail (suspend/reactivate + abonelik formu)/health;
PlatformAdmin değilse `/dashboard`'a yönlenir; dashboard sidebar'ında "Platform Yönetimi" linki + login yönlendirme.
**Bilinçli yapılmadı:** online ödeme, gerçek abonelik tahsilatı, fatura/e-arşiv, gerçek SMS/WhatsApp/e-posta,
Prometheus/gelişmiş metrik, multi-instance distributed lock, Excel/PDF export, kampanya/kupon, recurring booking,
büyük refactor, mevcut tenant uçlarında PlatformAdmin bypass. Detay: [platform-admin-and-observability.md](platform-admin-and-observability.md).

## Sprint 11 — Tamamlanan kapsam
**Yapıldı (pilot hazırlığı / QA / stabilizasyon — yeni domain feature YOK, migration YOK):** Gerçek tarayıcı
**E2E altyapısı** Playwright (Chromium headless, `pliaformaFrontend/playwright.config.ts` + `tests/e2e/*`, npm
`test:e2e`); **8 spec / 25 test** (auth-redirect 5 rol, owner dashboard, schedule modal'ları, student akışı,
instructor finans gizliliği, platform admin suspend/reactivate round-trip, notification bell okundu, mobil/Pixel-7
responsive + yatay taşma). **Deterministic seed** `scripts/e2e-seed.sh` (DEV/TEST-only; e2e-* verisini psql ile
sıfırlar, sonra sabit kimlikli PlatformAdmin/Owner/StudioAdmin/2 Instructor/Student/Guest + ClassType/availability/
seans/booking/attendance/paket/3 ödeme/telafi/bildirim + askıya alınabilir 2. stüdyo kurar). E2E **2x ardışık +
prod compose build** üzerinde 25/25 deterministic geçti. **UI/UX denetimi** (Playwright sweep): tüm owner/student/
platform ekranlarında **JS/page error YOK, yatay taşma YOK**; tek çıktı hard-reload'da beklenen `401→refresh`
(tasarım gereği, in-memory access + httpOnly refresh). **Auth-redirect polish:** stüdyo bağlamı olmayan
PlatformAdmin `/dashboard`'a girince `/platform`'a yönlendirilir (boş tenant dashboard'u yerine). **Prod config/env
audit** (11 madde, hepsi temiz): JWT fail-fast, rate limit prod 10 / dev 1000, same-origin/relative `/api`, nginx
`/api`+`/health` proxy, env example tam, DB conn env'den, worker prod ON / dev OFF, PlatformAdmin `SEED_ADMIN_*` ile.
**12 smoke regresyon + same-origin + compose dev/prod config + full `docker compose up --build` (smoke+seed+E2E :3000)**
hepsi yeşil. Yeni docs: `PILOT-CHECKLIST.md`, `TESTING.md`. **Bilinçli yapılmadı:** yeni domain entity/migration,
gerçek SMS/ödeme, büyük refactor, recurring, export, kampanya, UI redesign, çok-tarayıcı görsel regresyon, xUnit
birim testleri. Detay: [TESTING.md](TESTING.md), [PILOT-CHECKLIST.md](PILOT-CHECKLIST.md).

## Sprint 12 — Tamamlanan kapsam
**Yapıldı (release hardening / güvenlik / ops — yeni feature YOK, ürün kodu değişmedi):** **Repo/secret hygiene
audit** (gerçek secret YOK; yalnız placeholder + yerel-dev `postgres/postgres`; kök `.gitignore` eklendi:
`deploy/.env`, `*.pem/key/crt`, `*.sql/gz`, `ops/backups/`). **Security audit** → `docs/SECURITY-REVIEW.md`
(JWT fail-fast, refresh cookie HttpOnly+SameSite=Strict+Secure-prod+Path=/api/auth, access in-memory, **stack
trace sızıntısı YOK**, rate limit prod10/dev1000, tenant izolasyon, PlatformAdmin policy + bypass yok, body'den
StudioId güvenilmiyor). **`scripts/security-audit-smoke.sh`** (21 kontrol: cross-tenant by-id 404 + list no-leak,
instructor/student scope, platform bypass yok, notification cross-user 404, suspend write 403/read 200). **Backup/
restore** `ops/backup-postgres.sh` + `restore-postgres.sh` (credential'sız, env'den; `--clean` pg_dump + rotasyon)
+ `docs/BACKUP-RESTORE.md`; **compose DB'de gerçek test edildi** (368K dump → temp DB → doğru sayımlar → drop).
**nginx hardening:** secure headers (X-Content-Type-Options/X-Frame-Options/Referrer-Policy/Permissions-Policy) +
`server_tokens off` + body limit — **compose ağında canlı doğrulandı** (`Server: nginx` versiyon gizli, 4 başlık
yanıtta, `/`→frontend 200, `/health`→backend Healthy). **Validation runner** `run-smokes.sh` (13: 12 sprint +
security) / `run-e2e.sh` (seed+Playwright) / `run-full-validation.sh` (build+config+smoke+E2E). **Stabilizasyon
fix:** role-permission + sprint3 smoke'ları PGC auto-detect'e geçirildi (compose container adıyla da çalışır).
Yeni docs: `RELEASE-CHECKLIST.md`, `RELEASE-NOTES-MVP.md`, `SECURITY-REVIEW.md`, `BACKUP-RESTORE.md`. **Final
regresyon:** dotnet 0/0 + npm temiz + compose dev/prod config + **13 smoke (:3000 compose)** + **E2E 25/25
(compose prod build)** + nginx header doğrulama + backup/restore + full `docker compose up --build` — hepsi yeşil.
**Bilinçli yapılmadı:** yeni feature/entity, gerçek SMS/ödeme, export, recurring, büyük refactor, CSP (Next inline
tema scripti nonce ister — ertelendi), backend HTTP healthcheck (aspnet imajında araç yok). Detay:
[SECURITY-REVIEW.md](SECURITY-REVIEW.md), [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md), [RELEASE-NOTES-MVP.md](RELEASE-NOTES-MVP.md).

## Sprint 13 — Tamamlanan kapsam
**Yapıldı:** Gerçek **abonelik yaşam döngüsü + plan kataloğu + tahsilat defteri + lisans kontrolü** (gateway YOK;
tahsilat manuel "ödeme kaydet", Sprint 14 webhook aynı kontratı besler). Yeni entity: **`SubscriptionPlan`**
(dinamik katalog; IsDefault tekil filtered-unique; intro fiyat), **`PlatformSubscriptionPayment`** (ledger;
`Source: Manual|Gateway|Adjustment`; **öğrenci Payment'tan ayrı**). `PlatformSubscription` genişletildi
(SubscriptionPlanId snapshot, IntroMonthlyPrice/IntroPeriodsRemaining, **CancelAtPeriodEnd/CancelRequestedAt**).
**Lisans (dinamik, kalıcı job yok):** `SubscriptionRules.Evaluate` → effectiveStatus (Trialing/Active/
PastDueGrace/CancelPending/Expired/FailOpenLicensed) + isLicensed/isReadOnlyByLicense/daysRemaining/licenseMessage.
**`SubscriptionLicenseBehavior`** (pipeline: StudioStatus→**SubscriptionLicense**→MembershipGuard): lisanssız
yazma **403**, okuma+login açık; **admin-suspend'den bağımsız ikinci kapı**; **fail-open** (aboneliksiz=lisanslı,
ama overview/health **missingSubscriptionCount** + detayda uyarı ile görünür). **register-studio otomatik 14g
trial** (default plan; yoksa fail-etmez+log). **record-payment** = ledger satırı + dönem ilerletme **aynı
transaction** + intro fiyatı + bekleyen-cancel temizleme. **cancel-at-period-end** (Owner self `/api/studio/
subscription/cancel` + admin): Status değişmez, CancelAtPeriodEnd=true, dönem sonuna kadar lisanslı→sonra Expired.
**Fiyat snapshot** (plan değişse abonelik sabit). Migration `Sprint13_Subscriptions` + **default plan seed (4
paket: Başlangıç 1.490/Stüdyo 2.990 intro 1.990×3 default/Profesyonel 4.990/Kurumsal 7.990 teklif)** + **backfill
(mevcut tüm stüdyolar Active+1yıl → 260/260, kimse kilitlenmedi)**. Yetki: plan CRUD + abonelik yönetimi yalnız
**PlatformAdmin**; stüdyo abonelik görüntüleme/iptal yalnız **Owner** (StudioAdmin/Instructor/Student 403).
Frontend: `/platform/plans` (plan CRUD), `/dashboard/subscription` (Owner: durum+lisans+ödeme geçmişi+iptal),
global **LicenseBanner** (Owner, Active dışı durumlar), studio-detail abonelik kartı (plan ata/ödeme kaydet/iptal/
tahsilat geçmişi + missing uyarısı), overview/health missingCount + manuel tahsilat. smoke `sprint13-smoke.sh`
(31 kontrol) + **14 smoke (12 sprint + sprint13 + security)** + **28 E2E** (2x deterministic + prod compose) +
same-origin + full compose up --build yeşil. **Davranış değişikliği:** abonelik cancel artık cancel-at-period-end
(sprint10-smoke cancel assertion güncellendi). **Bilinçli yapılmadı:** gerçek kart/online tahsilat & webhook
(Sprint 14), fatura/e-arşiv, plan **limit zorlaması** (sakla/göster), gerçek SMS/e-posta. Detay:
[SPRINT-13-SPEC.md](SPRINT-13-SPEC.md), [platform-admin-and-observability.md](platform-admin-and-observability.md).

## Sprint 14 — Tamamlanan kapsam
**Yapıldı:** **Paket Planı V2 — ders grubu kapsamı + tarih-bazlı fiyat geçerlilikleri + booking paket uygunluğu**
(gerçek online ödeme YOK). Yeni entity: **`PackagePlanClassType`** (plan↔ClassType kapsamı), **`StudentPackageAllowedClassType`**
(satışta snapshot'lanan kapsam), **`PackagePlanPriceVersion`** (tarih-bazlı fiyat — **fiyatın source of truth'u**).
`PackagePlan` +`AllowAllClassTypes` (true→tüm gruplar, false→yalnız kapsam); `StudentPackage` +`AllowAllClassTypesSnapshot`;
`ClassBooking` +`ExpectedStudentPackageId`/`RequiresPayment`/`PackageEligibilityStatus`. **Booking eligibility (Regular
seans):** `PackageEligibilityChecker` → Eligible/NoPackageAtAll/NoBalance/WrongClassType; **kullanılabilir hak =
CurrentCreditBalance − kapanmamış (iptal edilmemiş + henüz consume edilmemiş) açık bookingler** → öğrenci hak sayısından
fazla rezervasyon yapamaz (kredi yine yoklamada düşer, **row-lock + aynı transaction**). Self/Instructor uygun değilse
**409**; Owner/StudioAdmin yazabilir → `RequiresPayment=true`. **Attendance consume:** açık paket > booking'in
`ExpectedStudentPackageId`'si > otomatik; seçilen paket ders grubunu kapsamalı (yoksa 409); beklenen paket
geçersiz/balance 0 → **sessiz fallback yok, 409**; başarılı consume `RequiresPayment`'ı temizler (**Resolved**).
**Snapshot:** satışta fiyat **computed effective price** (versiyondan) + kapsam snapshot'lanır → plan sonradan değişse de
mevcut paket etkilenmez. **Fiyat:** effective = `EffectiveFrom<=today` en güncel versiyon; `PackagePlan.Price` yalnız
cache (stale olsa bile satış computed fiyatı kullanır); gelecek zam job'suz otomatik geçer; DTO `currentEffectivePrice`/
`nextEffectivePrice`/`nextEffectiveFrom`. Endpoint: `GET/POST /api/studio/package-plans/{id}/price-versions`,
`GET /api/studio/package-plans/{id}`. Migration `Sprint14_PackagePlanV2` + **backfill** (AllowAllClassTypes=true,
AllowAllClassTypesSnapshot=true, RequiresPayment=false, her plana initial price version) → **30/30 plan versiyonlandı,
0 plan/paket kilitlenmedi**. Frontend: `/dashboard/package-plans` (ClassType multi-select + AllowAll toggle + mevcut/gelecek
fiyat), `book-classes` (uygun olmayan seans disabled + neden + "kullanılacak paket"), schedule rezervasyon listesinde
"Ödeme gerekli" rozeti. smoke `sprint14-smoke.sh` (**37 kontrol**) + **15 smoke (14 + sprint14)** + **31 E2E (28 + 3,
2x deterministic)** + same-origin (:3000) yeşil; backend 0/0, frontend build temiz (33 rota). **Bilinçli yapılmadı:**
gerçek gateway, rezervasyonda kredi fiilen düşürme/iade (kredi yoklamada düşer; booking yalnız hak rezerve eder), plan
limit zorlaması, otomatik fiyat-geçiş job'u. **Regresyon notu:** Regular self-booking artık geçerli paket ister →
sprint4/5/6 smoke'larına ilgili öğrenciye paket satışı eklendi (bilinçli, davranış değişikliği). Detay:
[SPRINT-14-SPEC.md](SPRINT-14-SPEC.md), [packages-and-payments.md](packages-and-payments.md).

## Pilot Handoff (deploy öncesi hazırlık)
**Yapıldı (yalnız doküman; kod değişmedi):** `FINAL-HANDOFF.md` + `PILOT-DEPLOY-DRY-RUN.md` (kopyala-yapıştır
komutlar + TLS checklist). **Infra versioning durumu netleştirildi:** kök git repo DEĞİL; `pliaformaApi` +
`pliaformaFrontend` ayrı repolar (`main`, **0 commit**); **compose + `deploy/` + `ops/` HİÇBİR repoda tracked
değil** → **pilot-öncesi BLOCKER**. Öneri: kök monorepo (A, pilot için önerilen) veya ayrı `pliaforma-infra`
repo (B). Tutarsızlık bulundu: tasarım docs (`design-system.md`, `architecture.md`) frontend reposunda → API
docs'tan relative link verilmez (FINAL-HANDOFF'ta düzeltildi). Detay: [FINAL-HANDOFF.md](FINAL-HANDOFF.md),
[PILOT-DEPLOY-DRY-RUN.md](PILOT-DEPLOY-DRY-RUN.md).
