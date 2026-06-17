# Yetkilendirme & Roller

## Roller

- **GlobalRole** (User): `Standard`, `PlatformAdmin`.
- **StudioRole** (StudioMembership): `Owner`, `StudioAdmin`, `Instructor`, `Student`.

## Rol matrisi (hedef — sprintlerde uygulanır)

| İşlem | PlatformAdmin | Owner | StudioAdmin | Instructor | Student |
|---|:--:|:--:|:--:|:--:|:--:|
| Stüdyo oluştur / askıya al | ✓ | – | – | – | – |
| Stüdyo ayarları, ClassType yönetimi | – | ✓ | ✓ | – | – |
| Hoca davet/çıkar, ücret | – | ✓ | ✓ | (kendi bio) | – |
| Müsaitlik / ders saati tanımla | – | ✓ | ✓ | ✓ (kendi) | – |
| Misafir öğrenci oluştur / User'a dönüştür | – | ✓ | ✓ | ✓ | – |
| Ders rezervasyonu (kendi) | – | – | – | – | ✓ |
| Vekaleten rezervasyon | – | ✓ | ✓ | ✓ | – |
| Yoklama işaretle | – | ✓ | ✓ | ✓ | – |
| Öğrenci aylık panel | – | ✓ | ✓ | ✓ (kendi öğr.) | ✓ (kendi) |
| Hoca iş hacmi paneli | – | ✓ | ✓ | ✓ (kendi) | – |

Owner/StudioAdmin stüdyoda her şeyi görür; Instructor/Student yalnızca kendini.

### Platform admin erişimi (Sprint 10)
- **Platform uçları** (`/api/platform/*`): yalnızca **PlatformAdmin** (`GlobalRole`). Policy
  `[Authorize(Policy="PlatformAdmin")]` JWT `global_role` claim'ini şart koşar (studio_role'e bağlı rol
  yetkisi değil); her handler ayrıca `EnsurePlatformAdmin` ile doğrular. Owner/StudioAdmin/Instructor/Student →
  **403**.
- **Stüdyo bağlamı gerektirmez:** PlatformAdmin aktif stüdyo seçmeden platform uçlarını kullanır.
- **Otomatik bypass YOK:** PlatformAdmin'in studio_id/studio_role claim'i yoksa normal tenant uçları 403 döner —
  cross-tenant erişim yalnızca platform prefix'i altında açıktır.
- **Suspend/Reactivate:** PlatformAdmin stüdyoyu askıya alır (yazma engellenir, veri silinmez) / aktifleştirir.
- Detay: [platform-admin-and-observability.md](platform-admin-and-observability.md).

### Bildirim erişimi (Sprint 9)
- **Kendi bildirimleri:** her kullanıcı (rol fark etmez) yalnızca **kendi** (aktif stüdyo) bildirimlerini
  görür/okur (`GET /api/notifications`, `/unread-count`, `POST {id}/read`, `/read-all`); başkasınınki → 404.
- **Manuel hatırlatma trigger** (`POST /api/studio/notifications/run-reminders`): **Owner/StudioAdmin**, yalnızca
  aktif stüdyo. Student/Instructor → 403.
- Detay: [notifications-and-reminders.md](notifications-and-reminders.md).

### Dashboard & rapor erişimi (Sprint 8)
- **Studio dashboard + raporlar** (`/api/studio/dashboard`, `/reports/payments`, `/reports/instructors`,
  `/instructors/{id}/dashboard`): yalnızca **Owner/StudioAdmin**.
- **Instructor** (`/api/instructor/dashboard`): yalnızca **kendi** iş hacmi + kendi tahmini kazancı; başka
  hocanınki 403/404.
- **Student** (`/api/student/dashboard`): yalnızca **kendi** verisi.
- **Staff öğrenci dashboard** (`/api/studio/students/{id}/dashboard`): Owner/Admin tümü (finans dahil);
  Instructor yalnızca erişebildiği öğrenci ve **finans gizli** (`paymentSummary` dahil edilmez).
- Detay: [dashboards-and-reports.md](dashboards-and-reports.md).

### Paket & ödeme erişimi (Sprint 7)
- **Yönetim (PackagePlan / StudentPackage / Payment):** yalnızca **Owner/StudioAdmin**. Instructor bu uçlara
  erişemez (403) ve finansal tutarları/öğrenci paket bakiyesini görmez (yoklama ekranı paket bilgisini
  Instructor'a doldurmaz). Instructor yoklamada **paket hakkı düşüremez** (403).
- **Öğrenci:** yalnızca **kendi** paket (`GET /api/student/packages`) ve ödeme (`GET /api/student/payments`)
  bilgisini görür; yönetim uçlarına erişemez.
- **Payment ile Package ayrı:** ödeme hak düşürmez; hak kullanımı yoklama/manuel ledger ile.
- Detay: [packages-and-payments.md](packages-and-payments.md).

### Yoklama erişimi (Sprint 6)
- **İşaretleme/görüntüleme:** Owner/StudioAdmin tüm seanslar; **Instructor yalnızca kendi seansı** (aksi 403);
  başka stüdyo → 404. **Student** staff yoklama uçlarına erişemez (403).
- **Excused/LateCancel telafi kredisi:** Instructor ancak politika `AllowInstructorManualGrant` açıksa verebilir
  (kapalıysa 403); Owner/StudioAdmin her zaman. Present/Absent kredi vermez.
- **Öğrenci:** yalnızca **kendi** katılım geçmişi + aylık özeti (`GET /api/student/attendance[/summary]`).
- Zaman: yalnızca başlamış/bitmiş seans (gelecek → 409, iptal seans/booking → 409).
- Detay: [attendance.md](attendance.md).

### Erteleme/iptal & telafi kredisi erişimi (Sprint 5)
- **Seans ertele/iptal:** Owner/StudioAdmin tüm hocalar; Instructor yalnızca kendi seansı (aksi 403);
  başka stüdyo → 404.
- **Toplu kapanış (closures):** yalnızca Owner/StudioAdmin (stüdyo geneli işlem; Instructor erişemez → 403).
- **Manuel kredi ver/geri al:** Owner/StudioAdmin tüm öğrenciler; Instructor yalnızca kendi oluşturduğu ya
  da kendi seansında rezervasyonu olan öğrenci, ve politika `AllowInstructorManualGrant` açıksa (aksi 403).
  Aylık manuel kredi limiti (vars. 2) aşılırsa 409.
- **Politika güncelle:** Owner/StudioAdmin (`PUT /api/studio/makeup-policy`); okuma tüm staff.
- **Öğrenci:** yalnızca **kendi** kredi bakiyesini/geçmişini görür (`GET /api/student/makeup-credits`);
  staff kredi uçlarına 403. Makeup seansı self-booking kredisi yoksa 409.
- Detay: [makeup-credits-and-postponements.md](makeup-credits-and-postponements.md).

### Rezervasyon erişimi (Sprint 4)
- Self: Student yalnızca **aktif kendi** StudentProfile'ı ile rezervasyon/iptal.
- Staff: Owner/StudioAdmin herhangi bir Active/Guest öğrenci; Instructor **yalnızca kendi seansı + kendi
  oluşturduğu öğrenci**. Inactive öğrenci için rezervasyon yok. Guest için self değil, staff OK.
- Cancel: Student kendi; Owner/Admin herhangi biri; Instructor kendi seansındakiler.
- Detay: [bookings-and-capacity.md](bookings-and-capacity.md).

### Müsaitlik & ders seansı erişimi (Sprint 3)
- Owner/StudioAdmin: tüm hocaların müsaitlik/seanslarını yönetir.
- Instructor: yalnızca **kendi** profili (`InstructorProfile.UserId == self`); başka hocanınkine 403.
- Student: bu yönetim uçlarına erişemez (403). Detay: [scheduling-and-availability.md](scheduling-and-availability.md).

### Öğrenci erişimi (Sprint 2)
- Owner/StudioAdmin: tüm öğrenciler; misafir oluştur/güncelle/dönüştür/pasifleştir.
- Instructor: yalnızca **kendi oluşturduğu** öğrenciler (`CreatedByUserId`); başka hocanınkine 403.
- Student: yönetim uçlarına erişemez (403); yalnızca `GET /api/student/me`.
- **HealthNotes** (hassas): Owner/StudioAdmin + oluşturan hoca + öğrencinin kendisi görür; aksi halde
  alan `null` döner (backend maskeler). Detay: [students-and-guest-flow.md](students-and-guest-flow.md).

## Sprint 0 durumu

Endpoint'ler `[Authorize]` (kimlik doğrulaması) ile korunur; rol bazlı kısıtlar (`studio_role`
claim'i, `RoleClaimType`) altyapısı hazırdır ve modül uçları geldikçe uygulanır.

## Token güvenliği

- Access token kısa ömürlü; imza HMAC-SHA256; `Jwt:Secret` **fail-fast** (≥32 bayt, user-secrets/env).
- Refresh token httpOnly cookie (`pf_refresh`, SameSite=Strict, path=/api/auth), DB'de SHA-256 hash,
  rotasyon + logout revoke.
- Auth uçlarında IP bazlı rate limit (dakikada 10).
