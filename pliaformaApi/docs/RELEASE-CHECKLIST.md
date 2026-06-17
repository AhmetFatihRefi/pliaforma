# Release Checklist (Pilot Cut)

Pilot yayınını **kesmek (cut)** için sıralı kontrol listesi. Operasyonel kurulum detayları:
[PILOT-CHECKLIST.md](PILOT-CHECKLIST.md) · Güvenlik: [SECURITY-REVIEW.md](SECURITY-REVIEW.md) · Yedek:
[BACKUP-RESTORE.md](BACKUP-RESTORE.md) · Test: [TESTING.md](TESTING.md).

## 1. Kod & checkpoint
- [ ] Çalışan ağaç temiz; iki repoda (pliaformaApi, pliaformaFrontend) **local commit/checkpoint** alındı.
      (Bu projede commit/push kullanıcı onayıyla yapılır.)
- [ ] Sürüm/etiket notu: hangi commit pilota gidiyor (her iki repo için SHA kaydı).

## 2. Ortam & secret
- [ ] `deploy/.env` oluşturuldu (`cp deploy/.env.example deploy/.env`), **gerçek secret'lar dolduruldu**.
- [ ] `JWT_SECRET = openssl rand -base64 48` (≥32 bayt). `POSTGRES_PASSWORD`, `SEED_ADMIN_PASSWORD` güçlü.
- [ ] `PUBLIC_ORIGIN=https://<domain>`. `NEXT_PUBLIC_API_URL` BOŞ (relative `/api`).
- [ ] `.env` commit edilmedi (kök `.gitignore` korur).

## 3. Build & config doğrulama
- [ ] `dotnet build pliaformaApi/pliaformaApi.sln` → 0/0.
- [ ] `cd pliaformaFrontend && npm run build` → temiz.
- [ ] `docker compose -f docker-compose.dev.yml config` ve `... prod.yml config` → exit 0.

## 4. DB yedek & migration dry-run
- [ ] (Mevcut DB varsa) `ops/backup-postgres.sh` ile **yedek alındı**.
- [ ] **Geri yükleme testi** geçici DB'ye yapıldı (bkz. BACKUP-RESTORE.md) — pilot öncesi ZORUNLU.
- [ ] Migration dry-run: staging/temiz DB'de `docker compose ... up` → backend açılışta migration'ları
      sorunsuz uyguladı (fail-fast: hata olursa konteyner açılmaz, logu izle).

## 5. Tam doğrulama (validation runner)
- [ ] Backend (:5132) + frontend (:3000) ayakta (native veya compose) + postgres konteyneri.
- [ ] `bash pliaformaApi/scripts/run-full-validation.sh` → build + compose config + **13 smoke
      (12 + security)** + **E2E (25)** hepsi ✅. (Aşamaları ayrı koşmak için `run-smokes.sh` / `run-e2e.sh`.)
- [ ] Same-origin: `run-smokes.sh http://localhost:3000` (Next/nginx proxy) ✅.

## 6. Compose up & sağlık
- [ ] `docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d --build`.
- [ ] `docker compose ... ps` → postgres healthy; backend/frontend/nginx up (restart: unless-stopped).
- [ ] `https://<domain>/health` → `{"status":"Healthy"}`.

## 7. İlk kullanıcı akışı
- [ ] **İlk PlatformAdmin** login (`SEED_ADMIN_*`) → `/platform` açılır.
- [ ] **İlk stüdyo** `register-studio` ile oluşturuldu; PlatformAdmin `/platform/studios`'ta görür.
- [ ] Her rol için **login testi** (Owner/Instructor/Student→/dashboard, PlatformAdmin→/platform; logout→/login).

## 8. TLS & nginx
- [ ] nginx 443/sertifika etkin; HTTP→HTTPS 301; Secure cookie HTTPS üzerinden çalışıyor.
- [ ] Güvenlik başlıkları yanıtta görünüyor (`curl -I https://<domain>` → X-Frame-Options, nosniff, Referrer-Policy).

## 9. Operasyon
- [ ] **Worker** prod'da açık (log: "Reminder worker başladı; interval=300s").
- [ ] **Rate limit** prod default (10/dk) — appsettings override YOK.
- [ ] **Günlük backup** cron kuruldu (off-site kopya) — bkz. BACKUP-RESTORE.md.
- [ ] **Demo/test verisi YOK**: `e2e-seed.sh` prod'da çalıştırılmadı (`e2e-*` kayıt yok).
- [ ] **Logging/monitoring**: `docker compose logs -f backend` (JSON) izleniyor; `/platform/health` &
      `/platform/overview` gözlemleniyor.

## 10. Rollback planı
- [ ] Hatalı deploy → `docker compose ... down` (asla `-v`) → önceki commit/image → `up -d --build`.
- [ ] Veri sorunu → backend'i durdur → `ops/restore-postgres.sh <son iyi yedek>` → backend'i başlat.
- [ ] Migration geri alma → `dotnet ef database update <öncekiMigration>` (ÖNCE yedek; veri kaybı riski).
- [ ] Stüdyo bazlı sorun → PlatformAdmin'den **suspend** (yazma durur, veri korunur) → düzelt → **reactivate**.

## 11. Pilot kullanıcıları
- [ ] Pilot stüdyo(lar) + iletişim kişileri listelendi; beklenti/geri bildirim kanalı belirlendi.
- [ ] Bilinen kısıtlar pilota iletildi (bkz. [RELEASE-NOTES-MVP.md](RELEASE-NOTES-MVP.md)).
