# Yedekleme & Geri Yükleme (PostgreSQL)

Pliaforma'nın tüm kalıcı verisi PostgreSQL'dedir (Docker named volume `pgdata`). Pilot/üretimde **düzenli
yedek + en az bir geri yükleme testi ZORUNLUDUR**. Scriptler `ops/` altındadır ve **gerçek credential
içermez** — her şey env'den gelir.

## Scriptler
- `ops/backup-postgres.sh` — `pg_dump --clean --if-exists` → `<db>_<UTC>.sql.gz`, rotasyon dahil.
- `ops/restore-postgres.sh` — gzip'li dump'ı hedef DB'ye uygular (yoksa oluşturur), kritik tabloları sayar.

## Yedek alma
```bash
PG_CONTAINER=pliaforma-prod-postgres-1 \
POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma \
RETENTION_DAYS=14 \
ops/backup-postgres.sh /var/backups/pliaforma
```
- `PG_CONTAINER`: postgres konteyner adı (`docker ps`). Dev: `pliaforma-postgres` / `pliaforma-dev-postgres-1`.
- Çıktı: `<db>_<UTC-zaman>.sql.gz`. `RETENTION_DAYS`'ten eski yedekler otomatik silinir (vars. 14).
- **Yedekleri VPS dışına kopyalayın** (S3/yedek disk) — tek sunucu çökerse yedek de kaybolmasın.

## Geri yükleme
> **UYARI:** Hedef DB'nin ÜZERİNE yazar. **Önce mevcut DB'nin yedeğini al.**
```bash
# Güvenli test: geçici bir DB'ye geri yükle (canlıya dokunmadan doğrula)
PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma \
ops/restore-postgres.sh /var/backups/pliaforma/pliaforma_20260101T030000Z.sql.gz pliaforma_restore_test

# Gerçek geri yükleme (felaket kurtarma): hedef = canlı DB (önce backend'i durdur)
docker compose -f docker-compose.prod.yml stop backend
PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma \
ops/restore-postgres.sh /var/backups/.../<dosya>.sql.gz
docker compose -f docker-compose.prod.yml start backend
```
Script sonunda `Users/Studios/StudioMemberships/Payments/Notifications` satır sayılarını yazar — beklenenle
karşılaştırın.

## Pilot öncesi geri yükleme testi (ZORUNLU)
1. Bir yedek al.
2. **Geçici bir DB'ye** (`pliaforma_restore_test`) geri yükle.
3. Tablo sayıları makul mü kontrol et.
4. Geçici DB'yi düşür: `docker exec <pg> dropdb -U <user> pliaforma_restore_test`.
> Bu döngü `docker-compose.dev.yml` üzerinde **doğrulanmıştır** (368K dump → temp DB → doğru sayımlar → drop).

## Günlük otomatik yedek (öneri)
VPS cron örneği (her gece 03:00 UTC):
```
0 3 * * * PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma \
  RETENTION_DAYS=30 /opt/pliaforma/ops/backup-postgres.sh /var/backups/pliaforma >> /var/log/pliaforma-backup.log 2>&1
```
- Migration ÖNCESİ ayrıca elle yedek al (geri dönüş için).
- Yedek dosyaları `.gitignore`'da (`*.sql`, `*.sql.gz`, `ops/backups/`, `backups/`) — repoya GİRMEZ.

## Volume notu
- `pgdata` Docker named volume kalıcıdır; `docker compose down` veriyi silmez. **`down -v` SİLER** — üretimde
  asla `-v` ile indirme.
- Volume bozulması/disk kaybı senaryosunda kurtarma yalnızca **dış yedektendir** → düzenli off-site kopya şart.
