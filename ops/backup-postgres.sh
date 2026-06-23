#!/usr/bin/env bash
# Pliaforma — PostgreSQL yedek (pg_dump). GERÇEK CREDENTIAL İÇERMEZ; her şey env'den gelir.
# Sprint 31D-2: BACKUP_ENCRYPTION_KEY verilirse yedek gpg (AES256) ile ŞİFRELENİR → .sql.gz.gpg.
# Kullanım:
#   PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma POSTGRES_DB=pliaforma \
#     BACKUP_ENCRYPTION_KEY='...' ops/backup-postgres.sh [HEDEF_DIZIN]
# Varsayılan hedef: ops/backups/  ·  Dosya: <db>_<UTC>.sql.gz.gpg (şifreli) | <db>_<UTC>.sql.gz (dev plain)
#
# Şifreleme kuralı:
#   - BACKUP_ENCRYPTION_KEY var → gpg ile şifreli (.sql.gz.gpg). gpg kurulu DEĞİLSE net hata.
#   - key yok + (Production VEYA REQUIRE_BACKUP_ENCRYPTION=1) → FAIL-FAST (prod'da şifresiz yedek YASAK).
#   - key yok + dev → UYARI + şifresiz .sql.gz (yalnız geliştirme).
# Passphrase argv'ye DÜŞMEZ (--passphrase-fd 3 + process substitution); temp key dosyası yok.
set -euo pipefail

PGC="${PG_CONTAINER:-pliaforma-postgres}"
DB="${POSTGRES_DB:-pliaforma}"
USER="${POSTGRES_USER:-postgres}"
OUT_DIR="${1:-$(dirname "$0")/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

command -v docker >/dev/null || { echo "docker bulunamadı." >&2; exit 2; }
docker ps --format '{{.Names}}' | grep -qx "$PGC" || { echo "Konteyner '$PGC' çalışmıyor." >&2; exit 2; }

# Prod mu? (şifrelemeyi zorunlu kılan koşullar)
is_prod() { [ "${ASPNETCORE_ENVIRONMENT:-}" = "Production" ] || [ "${REQUIRE_BACKUP_ENCRYPTION:-0}" = "1" ]; }

mkdir -p "$OUT_DIR"
STAMP=$(date -u +%Y%m%dT%H%M%SZ)

if [ -n "${BACKUP_ENCRYPTION_KEY:-}" ]; then
  command -v gpg >/dev/null || { echo "HATA: BACKUP_ENCRYPTION_KEY verilmiş ama gpg kurulu değil. Backup host'una gpg kurun." >&2; exit 3; }
  FILE="$OUT_DIR/${DB}_${STAMP}.sql.gz.gpg"
  MODE="ENCRYPTED (gpg AES256)"
  echo "Yedek alınıyor (şifreli): $PGC/$DB → $FILE"
  # pg_dump → gzip → gpg symmetric. Passphrase fd 3'ten (argv/loglara düşmez).
  docker exec "$PGC" pg_dump -U "$USER" --clean --if-exists "$DB" \
    | gzip \
    | gpg --symmetric --cipher-algo AES256 --batch --yes --pinentry-mode loopback --passphrase-fd 3 -o "$FILE" \
        3< <(printf '%s' "$BACKUP_ENCRYPTION_KEY")
else
  if is_prod; then
    echo "HATA: Production'da BACKUP_ENCRYPTION_KEY zorunlu — şifresiz yedek üretilmez (fail-fast)." >&2
    exit 3
  fi
  echo "UYARI: BACKUP_ENCRYPTION_KEY yok → ŞİFRESİZ yedek (yalnız geliştirme; prod'da yasak)." >&2
  FILE="$OUT_DIR/${DB}_${STAMP}.sql.gz"
  MODE="PLAIN (dev — şifresiz)"
  echo "Yedek alınıyor: $PGC/$DB → $FILE"
  docker exec "$PGC" pg_dump -U "$USER" --clean --if-exists "$DB" | gzip > "$FILE"
fi

SIZE=$(du -h "$FILE" | cut -f1)
echo "Tamam ($SIZE) — $MODE."

# Rotasyon: RETENTION_DAYS'ten eski yedekler (hem .sql.gz hem .sql.gz.gpg).
if [ "$RETENTION_DAYS" -gt 0 ]; then
  find "$OUT_DIR" -name "${DB}_*.sql.gz*" -type f -mtime "+$RETENTION_DAYS" -print -delete 2>/dev/null || true
fi

echo "Mevcut yedekler:"
ls -1t "$OUT_DIR"/${DB}_*.sql.gz* 2>/dev/null | head -10
