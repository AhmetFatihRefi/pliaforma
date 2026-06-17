# Pilot / Deployment Checklist

Pliaforma'yı küçük bir stüdyoyla **pilot** kullanıma almak için adım adım kontrol listesi. Hedef: tek VPS +
Docker Compose (nginx same-origin) ile production benzeri kurulum. (Bu sürüm: gerçek SMS/ödeme YOK; manuel
takip + in-app bildirim.)

## 0. Hazırlık (kararlar)
- [ ] **Domain** alındı, DNS A kaydı VPS IP'sine yönlendirildi (`pliaforma.example.com`).
- [ ] **VPS**: 2 vCPU / 2–4 GB RAM yeterli (küçük stüdyo). Docker + Docker Compose kurulu.
- [ ] Repo VPS'e klonlandı (backend + frontend tek repo kökünde compose dosyaları ile).

## 1. Ortam değişkenleri (kritik secret listesi)
`cp deploy/.env.example deploy/.env` → düzenle. **Hepsi zorunlu:**
- [ ] `POSTGRES_USER`, `POSTGRES_PASSWORD` (güçlü), `POSTGRES_DB`
- [ ] `JWT_SECRET` — **en az 32 bayt rastgele** (yoksa/kısaysa backend açılmaz, fail-fast). Üret:
      `openssl rand -base64 48`
- [ ] `JWT_ISSUER`, `JWT_AUDIENCE` (vars. Pliaforma)
- [ ] `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD` → **ilk PlatformAdmin** (aşağıya bkz.)
- [ ] `PUBLIC_ORIGIN=https://your-domain.com` (CORS allowlist + cookie/host)
> `.env` dosyasını ASLA commit etme. Secret'ları parola yöneticisinde sakla.

## 2. Docker
- [ ] `docker compose -f docker-compose.dev.yml config` ve `... prod.yml config` → exit 0 (geçerli).
- [ ] Build + ayağa kaldır:
      `docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d --build`
- [ ] `docker compose ... ps` → postgres healthy, backend/frontend/nginx up.
- [ ] Backend migration'ları açılışta otomatik uygular (fail-fast: hata olursa konteyner açılmaz — logu izle).

## 3. İlk PlatformAdmin
- [ ] `SEED_ADMIN_EMAIL` + `SEED_ADMIN_PASSWORD` set edildiyse backend açılışta **PlatformAdmin** tohumlar
      (idempotent; `GlobalRole=PlatformAdmin`). Log: "PlatformAdmin tohumlandı".
- [ ] `https://your-domain.com/login` → bu hesapla giriş → **`/platform`** paneline yönlenir.
- [ ] (Alternatif) Mevcut bir kullanıcıyı yükseltmek: DB'de
      `UPDATE "Users" SET "GlobalRole"='PlatformAdmin' WHERE "Email"='...';` → tekrar login.
- [ ] Pilot sonrası **seed admin parolasını değiştir** veya ayrı güçlü bir admin oluştur.

## 4. İlk stüdyo
- [ ] Stüdyo sahibine `https://your-domain.com/register-studio` ver → Owner + stüdyo oluşturur.
- [ ] PlatformAdmin `/platform/studios` listesinde yeni stüdyoyu görür.
- [ ] (Ops.) `/platform/studios/{id}` → **abonelik** kaydı oluştur (takip amaçlı; tahsilat yok).
- [ ] Owner stüdyo içinde: hoca davet, ClassType, müsaitlik, ders seansı, öğrenci ekler.

## 5. Demo/test verisi kapatma
- [ ] `scripts/e2e-seed.sh` **YALNIZCA dev/test**. Production DB'de ÇALIŞTIRMA (e2e-* hesapları/stüdyoları üretir).
- [ ] Prod'da yanlışlıkla seed çalıştıysa: `DELETE FROM "Studios" WHERE "OwnerUserId" IN
      (SELECT "Id" FROM "Users" WHERE "Email" LIKE 'e2e-%'); DELETE FROM "Users" WHERE "Email" LIKE 'e2e-%';`
- [ ] `SEED_ADMIN_*` dışında prod'da otomatik veri tohumlanmaz.

## 6. Sağlık & doğrulama
- [ ] `https://your-domain.com/health` → `{"status":"Healthy"}` (DB dahil).
- [ ] Smoke (staging/prod-benzeri ortamda): `bash pliaformaApi/scripts/sprint10-smoke.sh https://your-domain.com`
      (PlatformAdmin tohumlama için psql erişimi gerekir; yoksa yalnızca login/akış manuel doğrula).
- [ ] E2E (staging): `e2e-seed.sh` + `npm run test:e2e` (bkz. [TESTING.md](TESTING.md)).
- [ ] **Login testleri** (manuel, her rol): Owner→/dashboard, Instructor→/dashboard, Student→/dashboard,
      PlatformAdmin→/platform; logout→/login; yanlış parola→hata mesajı.

## 7. Bildirim worker
- [ ] Prod'da worker AÇIK (appsettings.json `Notifications:WorkerEnabled=true`, interval 300s).
- [ ] Açılış logunda "Reminder worker başladı; interval=300s." görülür.
- [ ] Askıya alınmış stüdyoya hatırlatma üretilmez (worker yalnızca Active stüdyoları tarar).
- [ ] Bu sürümde kanallar **InApp/Log** (gerçek SMS/WhatsApp/e-posta yok).

## 8. TLS / HTTPS
- [ ] `deploy/nginx.conf` HTTPS bloğunu etkinleştir (443 + sertifika). Sertifika: Let's Encrypt (certbot) veya
      sağlayıcı. compose'da 443 portunu ve `deploy/certs` mount'unu aç.
- [ ] HTTP→HTTPS 301 yönlendirmesi açık.
- [ ] httpOnly refresh cookie `Secure` üzerinden taşınır (HTTPS şart — pilot için en azından staging'de test et).

## 9. Yedekleme (DB backup)
- [ ] **Manuel yedek:** `docker exec <pg> pg_dump -U $POSTGRES_USER $POSTGRES_DB > backup_$(date +%F).sql`
- [ ] **Geri yükleme testi:** boş bir DB'ye `psql ... < backup.sql` ile en az bir kez denendi.
- [ ] **Günlük otomatik backup** önerisi: cron + `pg_dump` → tarihli dosya + 7–30 günlük rotasyon;
      yedekleri VPS dışına (S3/yedek disk) kopyala. (Bu sürümde otomatik yedek aracı dahil DEĞİL — operasyonel kurulum.)
- [ ] `pgdata` Docker volume kalıcıdır; `down -v` ile silinmemesine dikkat (veri kaybı).

## 10. Rollback notları
- [ ] Hatalı deploy: `docker compose ... down` → önceki image/commit'e dön → `up -d --build`.
- [ ] Migration geri alma: her migration `Down` içerir (`dotnet ef database update <ÖncekiMigration>`), ancak
      **veri kaybı riski** olabilir → önce DB yedeği al. Üretimde migration öncesi yedek ZORUNLU.
- [ ] Stüdyo bazlı sorun: PlatformAdmin'den ilgili stüdyoyu **suspend** et (yazma durur, veri korunur),
      düzeltip **reactivate** et — tüm sistemi durdurmadan izole müdahale.

## 11. Pilot sırasında izlenecekler
- [ ] `docker compose ... logs -f backend` (JSON log prod'da) — hata/exception takibi.
- [ ] `/platform/health` ve `/platform/overview` ile bildirim pending/failed + gecikmiş ödeme gözlemi.
- [ ] Disk/CPU/RAM (VPS) — küçük stüdyo için düşük; yine de izlenir.

## Kritik secret listesi (özet)
`JWT_SECRET` · `POSTGRES_PASSWORD` · `SEED_ADMIN_PASSWORD` · (TLS) sertifika özel anahtarı. Hiçbiri repoda/commit'te
olmamalı; `.env` + parola yöneticisi.
