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
| `sprint13-smoke.sh` | abonelik yaşam döngüsü + plan kataloğu + gelir defteri + lisans |
| `sprint16-smoke.sh` | e-posta doğrulama + şifre sıfırlama (log e-posta) |
| `sprint17-smoke.sh` | davet akışı + stüdyosuz kullanıcı (invite-only onboarding) |
| `sprint18a/18b-smoke.sh` | sözleşme-tabanlı program; Regular booking sözleşme zorunlu; self-booking 410 |
| `sprint19-smoke.sh` | ders onayı + fiilî hoca + hoca raporu (parasız); Present tamamlar, Absent→telafi |
| `sprint20-smoke.sh` | telafi akışı + closure + not-delivered telafisi; makeup add Owner/Admin |
| `sprint21-smoke.sh` | reschedule + chain-shift (telafi YOK); zincir atomik |
| `sprint22-smoke.sh` | grup düzeni değişikliği (telafisiz); preview=dry-run; atomik abort |
| `sprint23-smoke.sh` | sözleşme amendment + borç (hesaplanır); immutable geçmiş; currency mismatch 400 |
| `sprint24-smoke.sh` | öğrenci self-servis sözleşme/borç (salt-okunur); cross-student izolasyon; Instructor 403 |
| `sprint25-smoke.sh` | confirm scope (Instructor kendi dersi; confirm-today manager-only); pilot-gate regresyon |
| `security-audit-smoke.sh` | **release-gate**: yetki + tenant izolasyonu + platform bypass + suspend (konsolide) |

> `sprint7-smoke` ve `sprint14-smoke` paket modeli dormant olduğundan **deprecate** (dormancy korunur);
> `sprint15-smoke` self-apply JoinCode kaldırıldığından invite-only guard'a indirgendi.

Çalıştırma (tüm suite — kanonik koşucu, **27 smoke**):
```bash
cd pliaformaApi
bash scripts/run-smokes.sh http://localhost:5132   # hepsini sırayla koşar, özet verir
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

### Kapsam (16 spec, 56 test)
- **01-auth:** rol bazlı login yönlendirme, logout, korumalı sayfa oturumsuz→/login.
- **02-owner:** dashboard kartları + sidebar + /reports (parasız) + /students + /instructors (rate yok).
- **03-schedule:** ders kartı + onay/fiilî-hoca/işlenmedi + grup düzeni + reschedule mod A/B + rezervasyon/yoklama modalı.
- **04-student:** salt-okunur ekranlar + self-booking 410 + sidebar paket/self-service yok.
- **05-instructor:** dashboard + schedule + finans uçları yok + **'Bugünü onayla' görünmez (Sprint 25)**.
- **06-platform:** overview + studios + detay + suspend/reactivate + health + bypass→/platform.
- **07-notification-bell:** bell + dropdown + okundu + unread düşer.
- **08-responsive (mobil, Pixel 7):** /dashboard, /schedule, /platform/studios mobilde taşma yok.
- **09-subscription:** owner abonelik (auto-trial) + platform plan kataloğu + stüdyo gelir/abonelik.
- **10-package-v2:** paket UI yok (sözleşme ana akış) — deprecation doğrulaması.
- **11-self-service:** /register + check-email + öğrenci sözleşme/borç görünümü + JoinCode kartı yok.
- **12-email-auth:** şifre sıfırlama + reset/verify token state.
- **13-landing:** public landing hero + CTA'lar.
- **14-invitations:** Davetlerim + invite-only onboarding + hoca daveti kabul.
- **15-contracts:** 9999 sentinel gizli + sözleşme amendment + borç özeti.
- **16-makeup:** owner telafi özeti + öğrenci telafi salt-okunur.

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
