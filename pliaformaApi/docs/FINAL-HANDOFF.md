# Pliaforma — Final Handoff (Pilot Deploy Öncesi)

Tarih: 2026-06-17 · Durum: **MVP tamam, pilota hazır (1 infra blocker + TLS adımı insan eli bekliyor)**.
Bu doküman projeyi devralacak/deploy edecek kişi için tek giriş noktasıdır. Detay dokümanlara referans verir;
kopyalamaz.

---

## 1. Proje ne durumda?
Pilates stüdyoları için **multi-tenant SaaS**. 13 sprint (0–12) tamamlandı: çekirdek ürün + platform admin +
pilot hazırlığı + güvenlik/release hardening. Yeni feature **donduruldu** (freeze). Ürün kodu Sprint 12'den beri
değişmedi (tek istisna: dark-mode tarih ikonu için kozmetik CSS düzeltmesi — `globals.css` `color-scheme`,
build-doğrulandı, davranış/test etkilenmedi).

### Sprint 0–12 özet
| # | Konu |
|---|---|
| 0 | İskelet + Auth/Studio/Membership (JWT, refresh cookie, rate limit) + frontend shell |
| 1 | Üyelik, InstructorProfile+ücret, ClassType, hoca↔grup |
| 2 | StudentProfile + misafir + User'a dönüştürme |
| 3 | Müsaitlik + ClassSession (tek/bulk) |
| 4 | Rezervasyon + atomik kapasite (race-safe) |
| 5 | Erteleme/iptal + telafi kredisi ledger |
| 6 | Yoklama (attendance) |
| 7 | Paket + ödeme takibi (manuel) + attendance→paket düşüm |
| 8 | Rol bazlı dashboard + raporlar (read-only) |
| 9 | Bildirim + hatırlatma worker (in-app/log) |
| 10 | Platform admin + StudioStatus suspend + abonelik iskeleti + health |
| 11 | Pilot hazırlığı: Playwright E2E (25) + e2e-seed + prod config audit |
| 12 | MVP freeze: güvenlik denetimi + backup/restore + nginx hardening + validation runner |

Yol haritası ve sprint detayları: [sprints.md](sprints.md). Sürüm notları: [RELEASE-NOTES-MVP.md](RELEASE-NOTES-MVP.md).

---

## 2. Modüller
Kimlik & üyelik · hoca & grup (ClassType) · öğrenci & misafir · müsaitlik & ders seansı · rezervasyon &
kapasite · erteleme & telafi kredisi · yoklama · paket & ödeme (manuel takip) · dashboard & rapor · bildirim &
hatırlatma · platform admin (cross-tenant) & abonelik iskeleti.

## 3. Roller
- **PlatformAdmin** (global): tüm stüdyolar (`/platform`); suspend/reactivate, abonelik takibi (tahsilat yok),
  health. Tenant verisine yalnız `/api/platform/*` üzerinden.
- **Owner / StudioAdmin** (stüdyo): tam yönetim.
- **Instructor**: kendi programı/öğrencileri/yoklaması (finans gizli).
- **Student**: yalnız kendi verisi.

Yetki matrisi & güvenlik: [auth-and-roles.md](auth-and-roles.md), [SECURITY-REVIEW.md](SECURITY-REVIEW.md),
[multi-tenancy.md](multi-tenancy.md).

---

## 4. Backend nasıl çalışır?
.NET 10, Clean Architecture 5 katman (Domain/Application/Persistence/Infrastructure/Api), **özel CQRS Dispatcher**
(MediatR yok). Pipeline: `Logging → Validation → StudioStatus → MembershipGuard → Transaction → Handler`. Tenant
izolasyonu **handler seviyesinde** (`Id == x && StudioId == token.StudioId` → cross-tenant 404). PostgreSQL + EF
Core; migration'lar **açılışta otomatik** uygulanır (fail-fast). Solution: `pliaformaApi/pliaformaApi.sln`.
Çalıştırma (dev): `dotnet run --project src/Pliaforma.Api` (:5132). Mimari: [MIMARI-PLAN.md](MIMARI-PLAN.md).

## 5. Frontend nasıl çalışır?
Next.js 16 (App Router) + TS + Tailwind v4. **Same-origin**: API'ye relative `/api` ile gider (dev'de Next
rewrite, prod'da nginx proxy). Access token **bellekte**, refresh **httpOnly cookie**. Rol bazlı sidebar/sayfalar
+ ayrı `(platform)` route grubu. Çalıştırma (dev): `npm run dev` (:3000). Tasarım & mimari dokümanları **frontend
reposunda**: `pliaformaFrontend/docs/design-system.md` + `architecture.md` (ayrı repo → bu klasörden relative link
verilmez).

## 6. Docker nasıl çalışır?
- **Dev:** `docker-compose.dev.yml` (postgres + backend + frontend; :3000/:5132). Worker kapalı (determinism).
- **Prod:** `docker-compose.prod.yml` (postgres + backend + frontend + **nginx tek giriş**; yalnız nginx :80/:443
  dışarı). Same-origin; secure headers; secret'lar `deploy/.env`'den. Worker açık.
- **nginx:** `/`→frontend, `/api`→backend, `/health`→backend + güvenlik başlıkları (`deploy/nginx.conf`).

## 7. Testler nasıl çalışır?
- **API smoke** (`pliaformaApi/scripts/*.sh`): 12 sprint smoke + `security-audit-smoke.sh`.
- **E2E** (`pliaformaFrontend/tests/e2e/`, Playwright/Chromium): 25 test; önce `e2e-seed.sh`.
- **Runner'lar:** `run-smokes.sh` (13) · `run-e2e.sh` · `run-full-validation.sh` (build+config+smoke+E2E).
- Rehber: [TESTING.md](TESTING.md). Backup/restore: [BACKUP-RESTORE.md](BACKUP-RESTORE.md).

---

## 8. Infra Versioning (ÇÖZÜLDÜ — Seçenek A uygulandı)
Git durumu (güncel, Sprint 25):

| Klasör/dosya | Git durumu |
|---|---|
| **root `Pliaforma/`** | ✅ **root monorepo** (tek git repo, `main`, commit'li + etiketli) |
| `pliaformaApi/` (src, scripts, docs) | ✅ root repoda |
| `pliaformaFrontend/` (src, tests) | ✅ root repoda |
| `docker-compose.dev.yml` / `prod.yml` | ✅ tracked (root) |
| `deploy/` (nginx.conf, .env.example) | ✅ tracked (root) |
| `ops/` (backup/restore) | ✅ tracked (root) |
| `README.md`, `.gitignore` | ✅ tracked (root) |

> ✅ **ÇÖZÜLDÜ:** Eski "0 commit / infra versiyonlanmamış" blocker'ı artık geçerli değil. **Seçenek A (root
> monorepo)** uygulandı: `pliaformaApi/`, `pliaformaFrontend/`, `deploy/`, `ops/`, compose **tek repoda**.
> Tek `git clone` → compose relative build context'leri (`./pliaformaApi`, `./pliaformaFrontend`) olduğu gibi
> çalışır; stack'i ayağa kaldıran tüm dosyalar sürümlenir/review'lanır.
>
> Sürüm: Sprint 0–24 commit'li; en güncel etiket **v0.13.0-pilot**. Sprint 25 (pilot sertleştirme) ayrı koşuda.
> Pilot deploy: VPS'e tek `git clone` + `docker compose -f docker-compose.prod.yml up -d --build`
> (bkz. [PILOT-DEPLOY-DRY-RUN.md](PILOT-DEPLOY-DRY-RUN.md)).

**İleride (opsiyonel):** çok-takım/bağımsız ölçekleme gerekirse registry + CI ile image-tabanlı dağıtıma geçilebilir
(Seçenek B); pilot tek operatör + tek sunucu için monorepo yeterli ve en az hareketli parçaya sahip.

---

## 9. Deploy için gerekli dosyalar
- `docker-compose.prod.yml`, `deploy/nginx.conf`, `deploy/.env` (örnekten üretilir), `ops/backup-postgres.sh`,
  `ops/restore-postgres.sh`, + `pliaformaApi/`, `pliaformaFrontend/` (build context). Hepsinin **aynı kardeş
  düzende** bulunması şart (compose relative path'leri).

## 10. Production'da gerekli secret'lar (`deploy/.env`)
`POSTGRES_USER` · `POSTGRES_PASSWORD` · `POSTGRES_DB` · `JWT_SECRET` (≥32 bayt) · `JWT_ISSUER` · `JWT_AUDIENCE` ·
`SEED_ADMIN_EMAIL` · `SEED_ADMIN_PASSWORD` · `PUBLIC_ORIGIN=https://<domain>`. Örnek: `deploy/.env.example`
(yalnız placeholder; gerçek secret repoda YOK). `.env` commit edilmez (kök `.gitignore` korur).

## 11. İlk PlatformAdmin nasıl oluşturulur?
`deploy/.env`'de `SEED_ADMIN_EMAIL` + `SEED_ADMIN_PASSWORD` set et → backend açılışta idempotent bir
**PlatformAdmin** tohumlar (`GlobalRole=PlatformAdmin`). `https://<domain>/login` → bu hesapla gir → `/platform`.
Alternatif: mevcut kullanıcıyı yükselt: `UPDATE "Users" SET "GlobalRole"='PlatformAdmin' WHERE "Email"='...';`.

## 12. İlk stüdyo nasıl açılır?
Stüdyo sahibine `https://<domain>/register-studio` → Owner + stüdyo oluşur. PlatformAdmin `/platform/studios`'ta
görür; istenirse abonelik kaydı oluşturur (takip amaçlı). Owner içeride hoca/grup/öğrenci/seans ekler.

## 13. Pilot öncesi ZORUNLU kontroller
1. **Infra versioning blocker'ı çöz** (Seçenek A veya B) — Bölüm 8.
2. **TLS aç** (Bölüm 15 / dry-run) — Secure cookie HTTPS ister.
3. **DB yedek + restore testi** en az bir kez ([BACKUP-RESTORE.md](BACKUP-RESTORE.md)).
4. **`run-full-validation.sh`** yeşil + `/health` Healthy.
5. **Demo verisi YOK** (prod'da `e2e-seed.sh` çalıştırılmadı; `e2e-*` kayıt yok).
Tam liste: [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md), [PILOT-CHECKLIST.md](PILOT-CHECKLIST.md).

## 14. Bilinçli eksikler (MVP kapsamı dışı)
Gerçek SMS/WhatsApp/e-posta/push · gerçek online ödeme & abonelik tahsilatı & fatura · recurring booking · export
· kampanya/kupon · 2FA/hesap kilitleme · CSP başlığı · backend HTTP healthcheck · gelişmiş metrik (Prometheus) ·
worker tek-instance kilidi · paket otomatik expiry job · tam timezone modeli. Detay: [RELEASE-NOTES-MVP.md](RELEASE-NOTES-MVP.md).

## 15. TLS checklist (nginx 443)
`deploy/nginx.conf` içinde **HTTPS taslağı mevcut** (443 + sertifika + HTTP→HTTPS 301 — yorum satırlarında).
Pilot adımları → [PILOT-DEPLOY-DRY-RUN.md](PILOT-DEPLOY-DRY-RUN.md) "TLS" bölümü. Özet:
domain A kaydı → certbot sertifikası → nginx 443 bloğunu aç + certs mount + 443 portunu compose'da aç →
HTTP→HTTPS 301 → `PUBLIC_ORIGIN=https://<domain>` → Secure cookie doğrula → `/health` + `/api/auth` kontrol.

## 16. Final validation özeti (Sprint 12 son koşusu — yeniden çalıştırılmadı)
| Kontrol | Sonuç |
|---|---|
| `dotnet build` | ✅ 0/0 |
| `npm run build` | ✅ temiz (30 rota) |
| 13 smoke (12 sprint + security) | ✅ 13/13 (native :5132 + compose :3000) |
| security-audit-smoke | ✅ 21/21 |
| Playwright E2E | ✅ 25/25 (2x deterministic + compose prod build) |
| backup/restore | ✅ (368K dump → temp DB → doğru sayım → drop) |
| full `docker compose up --build` | ✅ |
| nginx security headers | ✅ canlı doğrulandı (4 başlık + `server_tokens off` + proxy) |
> Not: Bu sonuçlar Sprint 12 final regresyonundandır. O koşudan beri **tek değişiklik** dark-mode tarih ikonu
> CSS düzeltmesidir (kozmetik; `npm run build` ile doğrulandı; smoke/E2E mantığını etkilemez).

## 17. Sonraki gerçek feature adayları
1. **Gerçek bildirim kanalları** (SMS/e-posta sağlayıcı + sender registry + worker tek-instance kilidi).
2. **Gerçek abonelik/online ödeme tahsilatı** (gateway + webhook + otomatik PastDue→suspend).
3. **Güvenlik+**: CSP (nonce), 2FA, backend healthcheck, CI + bağımlılık tarama.
4. Recurring booking · paket expiry job · tam timezone modeli · export.
