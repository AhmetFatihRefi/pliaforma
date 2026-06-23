#!/usr/bin/env bash
# Pliaforma — PostgreSQL geri yükleme (pg_dump --clean çıktısından). GERÇEK CREDENTIAL İÇERMEZ.
# Sprint 31D-2: şifreli (.sql.gz.gpg) ve eski şifresiz (.sql.gz) yedekleri destekler.
# UYARI: Hedef DB'nin üzerine yazar. ÖNCE mevcut DB'nin yedeğini al (backup-postgres.sh).
# Kullanım:
#   PG_CONTAINER=pliaforma-prod-postgres-1 POSTGRES_USER=pliaforma [BACKUP_ENCRYPTION_KEY='...'] \
#     ops/restore-postgres.sh <yedek.sql.gz[.gpg]> [HEDEF_DB]
# HEDEF_DB verilmezse POSTGRES_DB (vars. pliaforma). Güvenli test için geçici bir DB adı verin.
set -euo pipefail

PGC="${PG_CONTAINER:-pliaforma-postgres}"
USER="${POSTGRES_USER:-postgres}"
FILE="${1:?Yedek dosyası gerekli: ops/restore-postgres.sh <yedek.sql.gz[.gpg]> [hedef_db]}"
TARGET_DB="${2:-${POSTGRES_DB:-pliaforma}}"

[ -f "$FILE" ] || { echo "Dosya yok: $FILE" >&2; exit 2; }
docker ps --format '{{.Names}}' | grep -qx "$PGC" || { echo "Konteyner '$PGC' çalışmıyor." >&2; exit 2; }

# Uzantı → kaynak SQL akışı (decrypt/decompress). Bilinmeyen uzantı → net hata.
case "$FILE" in
  *.sql.gz.gpg)
    [ -n "${BACKUP_ENCRYPTION_KEY:-}" ] || { echo "HATA: şifreli yedek (.sql.gz.gpg) için BACKUP_ENCRYPTION_KEY gerekli." >&2; exit 3; }
    command -v gpg >/dev/null || { echo "HATA: gpg kurulu değil (şifreli yedek açılamaz)." >&2; exit 3; }
    MODE="ENCRYPTED (gpg AES256)"
    ;;
  *.sql.gz)
    MODE="PLAIN (.sql.gz)"
    ;;
  *)
    echo "HATA: bilinmeyen uzantı '$FILE'. Beklenen: .sql.gz.gpg veya .sql.gz" >&2; exit 2
    ;;
esac

echo "!! Geri yükleme: $FILE ($MODE) → $PGC/$TARGET_DB (üzerine yazar)"
echo "   Devam etmeden önce mevcut '$TARGET_DB' yedeğinin alındığından emin olun."

# Şifreli/şifresiz yedeği düz SQL akışına çevirir. Yanlış key → gpg fail → pipefail ile restore DURUR.
emit_sql() {
  case "$FILE" in
    *.sql.gz.gpg)
      gpg --batch --yes --pinentry-mode loopback --passphrase-fd 3 -d "$FILE" \
        3< <(printf '%s' "${BACKUP_ENCRYPTION_KEY}") | gunzip ;;
    *.sql.gz)
      gunzip -c "$FILE" ;;
  esac
}

# Hedef DB yoksa oluştur (geçici/test DB senaryosu).
if ! docker exec "$PGC" psql -U "$USER" -tAc "SELECT 1 FROM pg_database WHERE datname='$TARGET_DB'" | grep -q 1; then
  echo "DB '$TARGET_DB' yok → oluşturuluyor."
  docker exec "$PGC" createdb -U "$USER" "$TARGET_DB"
fi

# Dump'ı hedefe uygula (--clean --if-exists dump içinde olduğundan nesneler önce düşürülür).
emit_sql | docker exec -i "$PGC" psql -U "$USER" -d "$TARGET_DB" -v ON_ERROR_STOP=0 >/dev/null

# Doğrulama: birkaç kritik tablo say.
echo "Geri yükleme tamamlandı. Doğrulama:"
for t in Users Studios StudioMemberships Payments Notifications; do
  c=$(docker exec "$PGC" psql -U "$USER" -d "$TARGET_DB" -tAc "SELECT count(*) FROM \"$t\";" 2>/dev/null || echo "?")
  printf "  %-20s %s\n" "$t" "$c"
done

# Migration history (bilgi amaçlı; drill'de zorunlu doğrulama yapılır).
LAST_MIG=$(docker exec "$PGC" psql -U "$USER" -d "$TARGET_DB" -tAc "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1;" 2>/dev/null | tr -d '\r' || echo "?")
echo "  son migration: ${LAST_MIG:-?}"
