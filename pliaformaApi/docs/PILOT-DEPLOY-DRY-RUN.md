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

## 3. Domain DNS
- Domain sağlayıcında **A kaydı**: `<domain>` → VPS public IP (örn. `203.0.113.10`).
- (Opsiyonel) `www` için ikinci A kaydı.
- Yayılmayı doğrula: `dig +short <domain>` → VPS IP dönmeli.

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
#   SEED_ADMIN_EMAIL=admin@<domain>
#   SEED_ADMIN_PASSWORD=<güçlü-admin-parolası>
#   PUBLIC_ORIGIN=https://<domain>
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

## 8. Health check (HTTP, TLS öncesi)
```bash
curl -fsS http://<domain>/health        # {"status":"Healthy", ...}
curl -fsS http://<domain>/ -o /dev/null -w "frontend: %{http_code}\n"
```

## 9. TLS / HTTPS (nginx 443)
`deploy/nginx.conf` içinde **443 + HTTP→HTTPS 301 taslağı yorum satırlarında hazır**. Adımlar:
```bash
# 9a. Sertifika (Let's Encrypt — webroot veya standalone). Standalone örnek (80 boşken):
docker compose --env-file deploy/.env -f docker-compose.prod.yml stop nginx
sudo apt-get -y install certbot
sudo certbot certonly --standalone -d <domain> --agree-tos -m admin@<domain> --no-eff-email
# Sertifikalar: /etc/letsencrypt/live/<domain>/{fullchain.pem,privkey.pem}

# 9b. Sertifikaları compose'un göreceği yere koy:
mkdir -p /opt/pliaforma/deploy/certs
sudo cp /etc/letsencrypt/live/<domain>/fullchain.pem /opt/pliaforma/deploy/certs/
sudo cp /etc/letsencrypt/live/<domain>/privkey.pem  /opt/pliaforma/deploy/certs/
sudo chown "$USER" /opt/pliaforma/deploy/certs/*.pem
```
**9c. `deploy/nginx.conf`**: dosyanın altındaki **HTTPS server bloğunu aç** (yorumdan çıkar): `server_name
<domain>`, `ssl_certificate /etc/nginx/certs/fullchain.pem`, `ssl_certificate_key /etc/nginx/certs/privkey.pem`,
yukarıdaki `location /`, `/api/`, `/health` + güvenlik başlıklarını aynen taşı; ayrıca
`server { listen 80; server_name <domain>; return 301 https://$host$request_uri; }` redirect bloğunu aç.
**9d. `docker-compose.prod.yml`**: nginx servisinde `- "443:443"` portunu ve `./deploy/certs:/etc/nginx/certs:ro`
mount'unu aç (yorumdan çıkar).
```bash
# 9e. nginx config testi + yeniden ayağa kaldır:
docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d
curl -fsS https://<domain>/health
curl -sI http://<domain>/ | grep -i location     # 301 → https beklenir
```

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

## 10. İlk login
- `https://<domain>/login` → **PlatformAdmin** (`SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD`) → `/platform`.
- `https://<domain>/register-studio` → ilk **Owner** + stüdyo. PlatformAdmin `/platform/studios`'ta görür.
- Rol login testi: Owner/Instructor/Student→`/dashboard`, logout→`/login`.

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
