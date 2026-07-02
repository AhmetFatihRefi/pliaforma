# Pilot Deploy — Dry-Run Planı

VPS'te sıfırdan pilot kurulum için **kopyala-yapıştır** komut dizisi. Gerçek secret YOK — `<...>` ve
`CHANGE_ME` yer tutucularını kendi değerlerinle doldur. Sıra: hazırlık → env → up → doğrula → TLS → yedek →
rollback. İlgili: [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) · [BACKUP-RESTORE.md](BACKUP-RESTORE.md) ·
[FINAL-HANDOFF.md](FINAL-HANDOFF.md).

> **ÖN KOŞUL (blocker):** compose + `deploy/` + `ops/` hiçbir repoda değil (bkz. FINAL-HANDOFF §8). Bu adımlar,
> bu dosyaların VPS'te **uygulamalarla aynı kardeş düzende** bulunduğunu varsayar. Önce infra versioning
> kararını ver (monorepo veya infra repo) ki `git clone` ile hepsi gelsin.

## 0. Beklenen dizin düzeni (VPS'te)
```
/opt/pliaforma/
├── pliaformaApi/            # backend (build context)
├── pliaformaFrontend/       # frontend (build context)
├── deploy/                  # nginx.conf, .env.example, certs/
├── ops/                     # backup-postgres.sh, restore-postgres.sh
├── docker-compose.prod.yml
└── deploy/.env              # SEN oluşturacaksın (commit edilmez)
```

## 1. VPS hazırlığı
```bash
# Ubuntu 22.04+ örneği. 2 vCPU / 2-4 GB RAM yeterli (küçük stüdyo).
sudo apt-get update && sudo apt-get -y upgrade
sudo timedatectl set-timezone UTC
sudo apt-get -y install ca-certificates curl git ufw
sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443 && sudo ufw --force enable
```

## 2. Docker kurulumu
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"      # tekrar login gerekebilir
docker --version && docker compose version
```

## 3. Domain DNS (Cloudflare — pliaforma.com)
Bu pilotun DNS'i Cloudflare'de, **DNS only / gri bulut** (proxy KAPALI). İlk deploy'da Cloudflare proxy
(turuncu bulut) ve Flexible SSL **KULLANILMAZ** — Let's Encrypt uçtan uca gerçek TLS kurulur. (Proxy sonradan
"Full (strict)" ile açılabilir — post-pilot.)
- A kayıtları (hepsi VPS IP → `185.184.210.54`): `pliaforma.com`, `app.pliaforma.com`, `api.pliaforma.com`.
- `www.pliaforma.com` → `pliaforma.com` (CNAME veya A).
- **Canonical host = `pliaforma.com`** (same-origin refresh cookie). nginx `www./app./api.` → 301 `pliaforma.com`
  (bkz. `deploy/nginx.conf` Sprint 49 domain bloğu). `app.`/`api.` ayrı origin OLARAK KULLANILMAZ (post-pilot).
- Yayılmayı doğrula: `dig +short pliaforma.com` → `185.184.210.54`. Cloudflare kaydında **Proxy status: DNS only**.

## 4. Kodu getir (infra versioning kararına göre)
```bash
sudo mkdir -p /opt/pliaforma && sudo chown "$USER" /opt/pliaforma
cd /opt/pliaforma
# Seçenek A (monorepo): tek clone
#   git clone <monorepo-url> .
# Seçenek B (3 repo): kardeş düzende clone
#   git clone <infra-url> .            # deploy/ ops/ compose buraya
#   git clone <backend-url> pliaformaApi
#   git clone <frontend-url> pliaformaFrontend
```

## 5. .env.production oluştur (deploy/.env)
```bash
cd /opt/pliaforma
cp deploy/.env.example deploy/.env
chmod 600 deploy/.env
# JWT secret üret (≥32 bayt) ve panoya değil dosyaya yaz:
echo "JWT_SECRET=$(openssl rand -base64 48)"   # çıktıyı deploy/.env'e taşı
# deploy/.env'i düzenle (nano deploy/.env):
#   POSTGRES_USER=pliaforma
#   POSTGRES_PASSWORD=<güçlü-parola>
#   POSTGRES_DB=pliaforma
#   JWT_SECRET=<openssl çıktısı>
#   JWT_ISSUER=Pliaforma
#   JWT_AUDIENCE=Pliaforma
#   SEED_ADMIN_EMAIL=admin@pliaforma.com
#   SEED_ADMIN_PASSWORD=<güçlü-admin-parolası>
#   PUBLIC_ORIGIN=https://pliaforma.com          # canonical host (same-origin cookie)
#   --- Pilot davranışı (Sprint 46/49) ---
#   PILOT_SIMPLE_PEOPLE_MODE=true                # bu tek-salon pilotu sade-mod kullanır (hoca guest öğrenci ekler)
#   STUDIO_REGISTRATION_ENABLED=false            # dışarıdan yeni stüdyo YOK (register-studio → 403). İlk owner: §10.
#   --- MFA (Sprint 31-MFA: Owner/StudioAdmin TOTP — PROD'da ZORUNLU) ---
#   MFA_ENABLED=true
#   MFA_ENCRYPTION_KEY=<openssl rand -base64 32>  # KRİTİK secret; ayrı yedekle (DB yedeğiyle aynı yerde DEĞİL)
#   --- Backup encryption (Sprint 31D-2 — PROD'da ZORUNLU) ---
#   BACKUP_ENCRYPTION_KEY=<openssl rand -base64 32>  # KRİTİK secret; DB yedeğiyle AYRI yerde sakla
#   --- E-posta (Sprint 16: doğrulama + şifre sıfırlama) ---
#   EMAIL_SENDER_MODE=Smtp            # PROD: Smtp önerilir. Log = yalnız dev/test. Disabled = prod'da KULLANMA.
#   EMAIL_FROM_ADDRESS=noreply@<domain>
#   EMAIL_FROM_NAME=Pliaforma
#   SMTP_HOST=<smtp-sunucu>           # Smtp iken ZORUNLU (yoksa backend AÇILMAZ — fail-fast)
#   SMTP_PORT=587
#   SMTP_USERNAME=<smtp-kullanıcı>
#   SMTP_PASSWORD=<smtp-parola>
#   SMTP_USE_TLS=true
# NOT: SenderMode=Smtp iken PublicOrigin + SMTP_HOST + EMAIL_FROM_ADDRESS eksikse backend açılmaz (fail-fast).
#      SenderMode=Disabled prod'da backend'i açmaz (doğrulama/sıfırlama e-postaları gitmez); bilinçliyse
#      Email__AllowDisabledInProduction=true gerekir — pilotta ÖNERİLMEZ.
```

## 6. Compose config doğrula
```bash
cd /opt/pliaforma
docker compose --env-file deploy/.env -f docker-compose.prod.yml config >/dev/null && echo "config OK"
```

## 7. Compose up (build)
```bash
docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d --build
docker compose --env-file deploy/.env -f docker-compose.prod.yml ps
# backend açılışta migration'ları uygular (fail-fast: hata olursa konteyner açılmaz)
docker compose --env-file deploy/.env -f docker-compose.prod.yml logs --tail=40 backend
```

## 8. Health check
```bash
# DİKKAT (Sprint 31B-1): HTTP/80 artık yalnız HTTPS'e 301 yönlendirir. Health'i HTTPS üzerinden doğrula:
curl -fsS https://<domain>/health        # {"status":"Healthy", ...}
curl -sI http://<domain>/ | grep -i location   # → 301 https://<domain>/ beklenir
curl -fsSk https://<domain>/ -o /dev/null -w "frontend: %{http_code}\n"
```

## 9. TLS / HTTPS (nginx 443)
**Sprint 31B-1 ile config ARTIK AKTİF:** `deploy/nginx.conf`'ta 443 ssl bloğu + 80→HTTPS 301 + HSTS açık;
`docker-compose.prod.yml`'de `443:443` portu + `./deploy/certs:/etc/nginx/certs:ro` mount açık. **Manuel iş
yalnızca gerçek sertifikayı sağlamaktır.** nginx, cert dosyaları yoksa AÇILMAZ (HTTPS zorunlu — bilinçli) →
**sertifikayı ilk `up`'tan ÖNCE yerleştir.**
```bash
# 9a. Sertifika (Let's Encrypt — standalone; 80 boşken, stack up'tan ÖNCE). Cloudflare DNS-only olmalı
#     (gri bulut), aksi halde standalone HTTP-01 doğrulaması Cloudflare'e takılır. TÜM host'lar tek cert'te:
sudo apt-get -y install certbot
sudo certbot certonly --standalone \
  -d pliaforma.com -d www.pliaforma.com -d app.pliaforma.com -d api.pliaforma.com \
  --agree-tos -m admin@pliaforma.com --no-eff-email
# Sertifikalar: /etc/letsencrypt/live/pliaforma.com/{fullchain.pem,privkey.pem} (SAN = 4 host)

# 9b. nginx'in beklediği yere kopyala (deploy/certs/{fullchain,privkey}.pem). Bu dosyalar repoya GİRMEZ
#     (.gitignore: deploy/certs/* hariç README). Detay: deploy/certs/README.md
mkdir -p /opt/pliaforma/deploy/certs
sudo cp /etc/letsencrypt/live/pliaforma.com/fullchain.pem /opt/pliaforma/deploy/certs/
sudo cp /etc/letsencrypt/live/pliaforma.com/privkey.pem  /opt/pliaforma/deploy/certs/
sudo chown "$USER" /opt/pliaforma/deploy/certs/*.pem

# 9c. (config zaten aktif — uncomment GEREKMEZ) up et ve doğrula:
docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d
curl -fsS https://<domain>/health
curl -sI http://<domain>/ | grep -i location     # 301 → https beklenir
```
> **HSTS notu:** nginx 443'te `Strict-Transport-Security` başlangıçta **kısa `max-age=300`** (HSTS sticky'dir;
> TLS bozuksa istemci kilitlenir). HTTPS uçtan uca doğrulandıktan sonra `deploy/nginx.conf`'ta `max-age=31536000`
> (gerekirse `preload`) yap.
> **Sertifika yenileme:** `certbot renew` sonrası `deploy/certs/`'i güncelle + `docker compose ... restart nginx`.

### TLS checklist (kısa)
- [ ] Domain A kaydı VPS IP'sine yöneldi (`dig +short <domain>`).
- [ ] certbot ile `<domain>` sertifikası alındı.
- [ ] nginx 443 bloğu + certs mount + 443 portu açıldı.
- [ ] HTTP→HTTPS 301 redirect çalışıyor (`curl -sI http://<domain>`).
- [ ] `PUBLIC_ORIGIN=https://<domain>` (deploy/.env) — backend yeniden başlatıldı.
- [ ] **Secure cookie doğrula:** `curl -i https://<domain>/api/auth/login -X POST -H 'Content-Type:
      application/json' -d '{"email":"<owner>","password":"<pw>"}'` → `Set-Cookie: pf_refresh=...; Secure;
      HttpOnly; SameSite=Strict; Path=/api/auth`.
- [ ] `https://<domain>/health` → Healthy. `/api/auth/me` (token ile) çalışıyor.
- [ ] Güvenlik başlıkları yanıtta: `curl -sI https://<domain>/ | grep -iE "X-Frame|nosniff|Referrer"`.

## 10. İlk login + ilk pilot owner (kayıt KAPALIYKEN)
`STUDIO_REGISTRATION_ENABLED=false` olduğundan `/register-studio` **403** döner (dışarıya kapalı). İlk pilot
stüdyo sahibini oluşturmanın **en güvenli / en az riskli** yolu — kısa süreli "aç → oluştur → kapat":

1. **PlatformAdmin** ile giriş: `https://pliaforma.com/login` → (`SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD`) → `/platform`.
   PlatformAdmin ilk girişte MFA kurar (prod'da zorunlu). Kurulum sonrası **`SEED_ADMIN_PASSWORD` değiştirin**
   veya bu hesabı yalnız acil kurtarma için saklayın.
2. **Kaydı geçici aç:** `deploy/.env` → `STUDIO_REGISTRATION_ENABLED=true` → yalnız backend'i yeniden başlat:
   `docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d backend`
   Doğrula: `curl -s https://pliaforma.com/api/auth/registration-status` → `{"studioRegistrationEnabled":true}`.
3. **Pilot Owner'ı sen (operatör) oluştur:** `https://pliaforma.com/register-studio` → stüdyo + Owner. Owner
   ilk girişte **MFA enroll** eder (recovery code'ları güvenli sakla). Bu adımı domain'i kimseye duyurmadan,
   hemen deploy sonrası yap (açık pencere saniyeler/dakikalar olsun).
4. **Kaydı tekrar kapat:** `deploy/.env` → `STUDIO_REGISTRATION_ENABLED=false` → `... up -d backend`.
   Doğrula: `curl -s https://pliaforma.com/api/auth/registration-status` → `{"studioRegistrationEnabled":false}`
   ve `curl -s -o /dev/null -w "%{http_code}" -X POST https://pliaforma.com/api/auth/register-studio -H 'Content-Type: application/json' -d '{}'` → **403**.
5. **Temizlik/doğrulama:** PlatformAdmin `/platform/studios` → yalnız amaçlanan pilot stüdyo görünmeli (varsa
   yanlış kayıtları not al; gerekiyorsa suspend). Rol login testi: Owner→`/dashboard`, logout→`/login`.

> Alternatif (kalıcı kapalı bırakıp DB'den owner açmak) ÖNERİLMEZ: parola hash'i + MFA state elle kurulmalı,
> hataya açık. Yukarıdaki "kısa pencere" yöntemi mevcut akışı (MFA enroll dahil) bozmadan kullanır.
> **Not:** stüdyosuz hesap (`/register`) ve davet-kabul akışı bu flag'ten ETKİLENMEZ — pilot hocaları Owner
> davet eder, hoca `/register` ile hesabını açıp daveti kabul eder (kayıt kapalıyken de çalışır).

## 11. Smoke test (staging/prod-benzeri)
```bash
# Postgres konteyner adı (PlatformAdmin tohumlama için psql erişimi gerekir):
docker ps --format '{{.Names}}' | grep postgres
# Tüm smoke (same-origin, nginx üzerinden):
bash pliaformaApi/scripts/run-smokes.sh https://<domain>
```
> Not: smoke'lar test verisi üretir; pilot-canlı DB'de çalıştırmak istemezsen ayrı bir staging ortamı kullan.

## 12. E2E test (staging)
```bash
cd pliaformaFrontend && npm ci && npx playwright install chromium
bash ../pliaformaApi/scripts/e2e-seed.sh https://<domain>     # DEV/TEST verisi — canlı pilota karıştırma!
npm run test:e2e
```

## 13. Yedek alma
```bash
PG=$(docker ps --format '{{.Names}}' | grep postgres | head -1)
PG_CONTAINER="$PG" POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma RETENTION_DAYS=30 \
  ops/backup-postgres.sh /var/backups/pliaforma
# Günlük cron (örnek):
#   0 3 * * * PG_CONTAINER=<pg> POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma RETENTION_DAYS=30 \
#     /opt/pliaforma/ops/backup-postgres.sh /var/backups/pliaforma >> /var/log/pliaforma-backup.log 2>&1
```

## 14. Restore testi (ZORUNLU, geçici DB'ye)
```bash
PG=$(docker ps --format '{{.Names}}' | grep postgres | head -1)
BK=$(ls -1t /var/backups/pliaforma/pliaforma_*.sql.gz | head -1)
PG_CONTAINER="$PG" POSTGRES_USER=pliaforma ops/restore-postgres.sh "$BK" pliaforma_restore_test
docker exec "$PG" dropdb -U pliaforma pliaforma_restore_test    # test bitince temizle
```

## 15. Rollback planı
```bash
# Hatalı deploy → önceki sürüme dön (ASLA -v ile down → veri kaybı):
docker compose --env-file deploy/.env -f docker-compose.prod.yml down
git -C pliaformaApi checkout <önceki-sha> ; git -C pliaformaFrontend checkout <önceki-sha>
docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d --build

# Veri sorunu → backend durdur, son iyi yedeği geri yükle, başlat:
docker compose ... stop backend
PG_CONTAINER=<pg> POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma ops/restore-postgres.sh <son-iyi-yedek>
docker compose ... start backend

# Migration geri alma (ÖNCE yedek!):
#   dotnet ef database update <öncekiMigration>   (veya yedekten restore)

# Stüdyo bazlı sorun → PlatformAdmin'den suspend (yazma durur, veri korunur) → düzelt → reactivate.
```

## 16. Pilot sırasında izleme
```bash
docker compose --env-file deploy/.env -f docker-compose.prod.yml logs -f backend   # JSON log
# /platform/health ve /platform/overview ile bildirim pending/failed + gecikmiş ödeme gözlemi
```
