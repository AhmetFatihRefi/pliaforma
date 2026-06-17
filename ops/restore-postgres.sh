#!/usr/bin/env bash
# Pliaforma — PostgreSQL geri yükleme (pg_dump --clean çıktısından). GERÇEK CREDENTIAL İÇERMEZ.
# UYARI: Hedef DB'nin üzerine yazar. ÖNCE mevcut DB'nin yedeğini al (backup-postgres.sh).
# Kullanım:
#   PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma \
#     ops/restore-postgres.sh <yedek.sql.gz> [HEDEF_DB]
# HEDEF_DB verilmezse POSTGRES_DB (vars. pliaforma). Güvenli test için geçici bir DB adı verin.
set -euo pipefail

PGC="${PG_CONTAINER:-pliaforma-postgres}"
USER="${POSTGRES_USER:-postgres}"
FILE="${1:?Yedek dosyası gerekli: ops/restore-postgres.sh <yedek.sql.gz> [hedef_db]}"
TARGET_DB="${2:-${POSTGRES_DB:-pliaforma}}"

[ -f "$FILE" ] || { echo "Dosya yok: $FILE" >&2; exit 2; }
docker ps --format '{{.Names}}' | grep -qx "$PGC" || { echo "Konteyner '$PGC' çalışmıyor." >&2; exit 2; }

echo "!! Geri yükleme: $FILE → $PGC/$TARGET_DB (üzerine yazar)"
echo "   Devam etmeden önce mevcut '$TARGET_DB' yedeğinin alındığından emin olun."

# Hedef DB yoksa oluştur (geçici/test DB senaryosu).
if ! docker exec "$PGC" psql -U "$USER" -tAc "SELECT 1 FROM pg_database WHERE datname='$TARGET_DB'" | grep -q 1; then
  echo "DB '$TARGET_DB' yok → oluşturuluyor."
  docker exec "$PGC" createdb -U "$USER" "$TARGET_DB"
fi

# Dump'ı hedefe uygula (--clean --if-exists dump içinde olduğundan nesneler önce düşürülür).
gunzip -c "$FILE" | docker exec -i "$PGC" psql -U "$USER" -d "$TARGET_DB" -v ON_ERROR_STOP=0 >/dev/null

# Doğrulama: birkaç kritik tablo say.
echo "Geri yükleme tamamlandı. Doğrulama:"
for t in Users Studios StudioMemberships Payments Notifications; do
  c=$(docker exec "$PGC" psql -U "$USER" -d "$TARGET_DB" -tAc "SELECT count(*) FROM \"$t\";" 2>/dev/null || echo "?")
  printf "  %-20s %s\n" "$t" "$c"
done
