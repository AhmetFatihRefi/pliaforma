#!/usr/bin/env bash
# Pliaforma — PostgreSQL yedek (pg_dump). GERÇEK CREDENTIAL İÇERMEZ; her şey env'den gelir.
# Kullanım:
#   PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma \
#     ops/backup-postgres.sh [HEDEF_DIZIN]
# Varsayılan hedef: ops/backups/  ·  Dosya: <db>_<UTC-zaman>.sql.gz
set -euo pipefail

PGC="${PG_CONTAINER:-pliaforma-postgres}"
DB="${POSTGRES_DB:-pliaforma}"
USER="${POSTGRES_USER:-postgres}"
OUT_DIR="${1:-$(dirname "$0")/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

command -v docker >/dev/null || { echo "docker bulunamadı." >&2; exit 2; }
docker ps --format '{{.Names}}' | grep -qx "$PGC" || { echo "Konteyner '$PGC' çalışmıyor." >&2; exit 2; }

mkdir -p "$OUT_DIR"
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
FILE="$OUT_DIR/${DB}_${STAMP}.sql.gz"

echo "Yedek alınıyor: $PGC/$DB → $FILE"
# --clean --if-exists: restore sırasında nesneleri güvenle yeniden oluşturur.
docker exec "$PGC" pg_dump -U "$USER" --clean --if-exists "$DB" | gzip > "$FILE"

SIZE=$(du -h "$FILE" | cut -f1)
echo "Tamam ($SIZE)."

# Rotasyon: RETENTION_DAYS'ten eski yedekleri sil.
if [ "$RETENTION_DAYS" -gt 0 ]; then
  find "$OUT_DIR" -name "${DB}_*.sql.gz" -type f -mtime "+$RETENTION_DAYS" -print -delete 2>/dev/null || true
fi

echo "Mevcut yedekler:"
ls -1t "$OUT_DIR"/${DB}_*.sql.gz 2>/dev/null | head -10
