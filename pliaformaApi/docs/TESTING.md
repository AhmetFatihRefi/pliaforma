# Test Rehberi (Smoke + E2E)

Pliaforma'nın iki test katmanı vardır: **API smoke** (bash/curl, backend davranışı) ve **E2E**
(Playwright, gerçek tarayıcı, same-origin `/api` üzerinden uçtan uca). Her ikisi de **dev/test** ortamına
karşı çalışır; gerçek dış servis (SMS/ödeme) ÇAĞIRMAZ.

## 1. Önkoşullar
- PostgreSQL çalışıyor (yerel docker `pliaforma-postgres` ya da dev compose `pliaforma-dev-postgres`).
- Backend `:5132` (native `dotnet run` veya compose).
- E2E için ayrıca frontend `:3000` (native `npm run dev` veya compose).
- `Jwt:Secret` dev'de user-secrets/compose env ile sağlanmış olmalı (yoksa backend açılmaz — fail-fast).

## 2. API Smoke testleri
Konum: `pliaformaApi/scripts/*.sh`. Her biri kendi verisini API ile kurar; bazıları DB'yi `docker exec psql`
ile düzenler (geçmişe çekme, PlatformAdmin rolü). Varsayılan BASE `http://localhost:5132`; same-origin
doğrulaması için `http://localhost:3000` (Next proxy) verilebilir.

| Script | Neyi garanti eder |
|---|---|
| `auth-smoke.sh` | kayıt/login/refresh/logout/switch-studio, cookie, tenant temel |
| `sprint1-smoke.sh` | üyelik, InstructorProfile, ClassType, ilk write guard + tenant 404 |
| `role-permission-smoke.sh` | rol kapıları (Owner/StudioAdmin/Instructor/Student) |
| `sprint2-smoke.sh` | öğrenci/misafir, User'a dönüştürme (Id korunur), hoca scope |
| `sprint3-smoke.sh` | müsaitlik + ders seansı, çakışma/geçmiş/availability kuralları |
| `sprint4-smoke.sh` | rezervasyon + **atomik kapasite** (paralel yarış: cap=1'e 5 → 1 başarılı) |
| `sprint5-smoke.sh` | erteleme/iptal + telafi kredisi ledger + makeup tüketim |
| `sprint6-smoke.sh` | yoklama (zaman/rol/tenant), Excused/LateCancel kredisi |
| `sprint7-smoke.sh` | paket + ödeme + attendance→paket düşüm (idempotent) |
| `sprint8-smoke.sh` | rol bazlı dashboard + rapor + finans gizliliği |
| `sprint9-smoke.sh` | bildirim üretimi + idempotency + in-app okuma + yetki |
| `sprint10-smoke.sh` | platform admin + suspend/reactivate + abonelik + health + tenant bypass yok |

Çalıştırma (tümü):
```bash
cd pliaformaApi
for s in auth sprint1 role-permission sprint2 sprint3 sprint4 sprint5 sprint6 sprint7 sprint8 sprint9 sprint10; do
  bash scripts/${s/sprint/sprint}-smoke.sh || echo "FAIL: $s"
done
```
> **Not (Windows/Git Bash):** curl Türkçe karakterleri (ş/ı) geçersiz UTF-8 gönderebilir → smoke gövdelerinde
> ASCII metin kullanılır (backend doğru; bu bir test-script kısıtıdır).

## 3. E2E (Playwright, gerçek tarayıcı)
Konum: `pliaformaFrontend/tests/e2e/*.spec.ts` + `playwright.config.ts`. Chromium **headless**, tek worker
(deterministic), `baseURL=http://localhost:3000` (same-origin `/api`). Sunucuları Playwright BAŞLATMAZ.

### Kurulum (ilk sefer)
```bash
cd pliaformaFrontend
npm install            # @playwright/test devDependency
npx playwright install chromium
```

### Akış (her koşudan önce seed)
```bash
# 1) backend (:5132) + frontend (:3000) çalışır olmalı
# 2) deterministic demo veriyi kur (DEV/TEST):
bash ../pliaformaApi/scripts/e2e-seed.sh
# 3) testleri çalıştır:
npm run test:e2e            # headless
npm run test:e2e:headed     # görünür tarayıcı (debug)
npm run test:e2e:report     # son HTML raporu
```

### Kapsam (8 spec, 25 test)
- **01-auth:** rol bazlı login yönlendirme (Owner/Student/Instructor→/dashboard, PlatformAdmin→/platform), logout, korumalı sayfa oturumsuz→/login.
- **02-owner:** dashboard kartları + sidebar + /reports + /students (seed öğrenci) + /instructors.
- **03-schedule:** ders kartı + rezervasyonlar modalı + yoklama modalı.
- **04-student:** book-classes/my-bookings/my-packages/my-payments/my-attendance/notifications + finans linkleri yok + seed bildirimi.
- **05-instructor:** dashboard + schedule + finans uçları yok.
- **06-platform:** overview + studios + detay + **suspend/reactivate round-trip** + health + /dashboard bypass→/platform.
- **07-notification-bell:** bell + dropdown + okundu işaretleme + unread düşer.
- **08-responsive (mobil, Pixel 7):** /dashboard, /schedule, /platform/studios mobilde ana içerik + yatay taşma yok.

## 4. Full compose ile test
```bash
cd <repo kökü>
docker compose -f docker-compose.dev.yml up --build -d   # postgres+backend+frontend
# hazır olunca:
bash pliaformaApi/scripts/sprint10-smoke.sh http://localhost:3000   # smoke (Next proxy)
bash pliaformaApi/scripts/e2e-seed.sh                               # seed (PGC=pliaforma-dev-postgres)
cd pliaformaFrontend && npm run test:e2e                            # e2e :3000
docker compose -f docker-compose.dev.yml down
```

## 5. Hangi test neyi garanti EDER / ETMEZ
**Eder:** API iş kuralları + yetki + tenant izolasyonu (smoke); rol bazlı UI akışları, yönlendirme, modal açılışı,
same-origin `/api`, mobil ana içerik + taşma (E2E). Build (dotnet 0/0, next temiz) + migration uygulanır.

**Etmez (bu sürümde):**
- Gerçek SMS/WhatsApp/e-posta/push gönderimi (no-op; test edilmez).
- Gerçek online ödeme / abonelik tahsilatı (yok).
- Yük/performans/penetrasyon testi.
- Çok tarayıcılı görsel regresyon (yalnızca Chromium; piksel-karşılaştırma yok).
- E2E her koşudan önce **seed** ister; seed çalıştırılmadan ardışık koşularda okundu-durumu kayar (determinism seed'e bağlı).
- Otomatik birim/entegrasyon testi (xUnit vb.) yoktur; doğrulama smoke + E2E iledir.

## 6. Flaky/kırılganlık notu
- Playwright `retries: 1` ile dev-server **soğuk derleme** gecikmesini absorbe eder (flaky maskeleme değil — ilk
  ziyaret derlemesi). İki ardışık seed+koşu deterministic geçer.
- E2E sunucu tarafında veri DEĞİŞTİREN iki test vardır (bell okundu; studio suspend→reactivate). İkisi de
  kendini geri alır/seed sıfırlar; tek-worker sıralı koşu çakışmayı önler.
