#!/usr/bin/env bash
# Pliaforma — Sprint 31D-2 restore drill (yarı-otomatik). Throwaway key ile şifreli yedek al → şifreli olduğunu
# doğrula → yanlış key FAIL → doğru key OK → izole temp DB'ye restore → migration history + kritik tablolar +
# SecurityAuditLogs + MFA tabloları doğrula → temp DB drop. GERÇEK key kullanmaz/yazmaz.
# App-health + smoke subset bu script'te DEĞİL (runbook'ta opsiyonel/manuel adım).
# Kullanım: PG_CONTAINER=pliaforma-dev-postgres-1 POSTGRES_DB=pliaforma POSTGRES_USER=postgres ops/restore-drill.sh
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
DB="${POSTGRES_DB:-pliaforma}"
USER="${POSTGRES_USER:-postgres}"
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
command -v gpg >/dev/null || { echo "gpg yok — drill çalıştırılamaz." >&2; exit 2; }
[ -z "$PGC" ] && { echo "postgres konteyneri yok (PG_CONTAINER ver)." >&2; exit 2; }

DRILL_DIR="$(mktemp -d)"
KEY="$(openssl rand -base64 32)"            # throwaway — yalnız bu çalıştırmada, repo'ya/diske kalıcı yazılmaz
TARGET="pliaforma_drill_$(date +%s)"
q(){ docker exec "$PGC" psql -U "$USER" -d "$TARGET" -tAc "$1" 2>/dev/null | tr -d '\r'; }
cleanup(){ docker exec "$PGC" psql -U "$USER" -c "DROP DATABASE IF EXISTS \"$TARGET\";" >/dev/null 2>&1; rm -rf "$DRILL_DIR"; }
trap cleanup EXIT

echo "== 1) Şifreli yedek al (throwaway key) =="
BACKUP_ENCRYPTION_KEY="$KEY" PG_CONTAINER="$PGC" POSTGRES_DB="$DB" POSTGRES_USER="$USER" RETENTION_DAYS=0 \
  bash "$DIR/backup-postgres.sh" "$DRILL_DIR" >/dev/null 2>&1
FILE="$(ls -1t "$DRILL_DIR"/*.sql.gz.gpg 2>/dev/null | head -1)"
[ -n "$FILE" ] && [ -f "$FILE" ] && ok "şifreli yedek oluştu (.sql.gz.gpg)" || { bad "şifreli yedek oluşmadı"; exit 1; }

echo "== 2) Şifreli mi? (düz okunamaz) =="
if gunzip -t "$FILE" 2>/dev/null; then bad "dosya düz gzip (şifreli DEĞİL!)"; else ok "düz gzip değil (şifreli)"; fi
if grep -qaiE "CREATE TABLE|INSERT INTO|COPY \"" "$FILE" 2>/dev/null; then bad "şifreli dosyada düz SQL bulundu!"; else ok "şifreli dosyada düz SQL bulunamadı"; fi

echo "== 3) Yanlış key decrypt FAIL =="
if gpg --batch --yes --pinentry-mode loopback --passphrase-fd 3 -d "$FILE" 3< <(printf '%s' "WRONG-${KEY}") >/dev/null 2>&1; then bad "yanlış key decrypt ETTİ (olmamalı)"; else ok "yanlış key decrypt edemedi"; fi

echo "== 4) Doğru key decrypt OK =="
# grep -c (tümünü okur) → grep -q erken-çıkışının SIGPIPE'ı pipefail'i tetiklemesin.
DEC=$(gpg --batch --yes --pinentry-mode loopback --passphrase-fd 3 -d "$FILE" 3< <(printf '%s' "$KEY") 2>/dev/null | gunzip 2>/dev/null | grep -ciE "PostgreSQL database dump|CREATE TABLE|SET ")
[ "${DEC:-0}" -gt 0 ] && ok "doğru key decrypt + gunzip → düz SQL" || bad "doğru key ile çözülemedi"

echo "== 5) İzole temp DB'ye restore ($TARGET) =="
if BACKUP_ENCRYPTION_KEY="$KEY" PG_CONTAINER="$PGC" POSTGRES_USER="$USER" bash "$DIR/restore-postgres.sh" "$FILE" "$TARGET" >/dev/null 2>&1; then ok "temp DB restore başarılı"; else bad "temp DB restore başarısız"; fi

echo "== 6) Doğrulama (migration history + tablolar) =="
LAST=$(q "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1;")
# Sprint 47: hard-coded migration adı yerine repo'daki GÜNCEL son migration dosyası referans alınır — her yeni
# sprint migration eklendiğinde bu satır elle güncellenmek ZORUNDA kalınmaz (eski pin unutulup yanlış FAIL vermez).
MIGRATIONS_DIR="$DIR/../pliaformaApi/src/Pliaforma.Persistence/Migrations"
EXPECTED_LAST=$(ls -1 "$MIGRATIONS_DIR" 2>/dev/null | grep -v '\.Designer\.cs$\|ModelSnapshot\.cs$' | sed 's/\.cs$//' | sort | tail -1)
if [ -n "$EXPECTED_LAST" ]; then
  [ "$LAST" = "$EXPECTED_LAST" ] && ok "son migration repo ile uyumlu ($LAST)" || bad "migration history repo'dan farklı: DB='${LAST:-yok}' repo='$EXPECTED_LAST'"
else
  echo "$LAST" | grep -q "." && ok "son migration mevcut ($LAST) — repo Migrations klasörü bulunamadı, karşılaştırma atlandı" || bad "migration history boş"
fi
for t in Users Studios Payments SecurityAuditLogs UserMfaSettings MfaRecoveryCodes; do
  c=$(q "SELECT count(*) FROM \"$t\";")
  case "$c" in (''|*[!0-9]*) bad "$t restore edilemedi (sayım='$c')";; (*) ok "$t restore (n=$c)";; esac
done

echo
[ "$FAILED" = "0" ] && { printf "\033[36mRESTORE DRILL: TÜM KONTROLLER GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mRESTORE DRILL: BAŞARISIZ ❌\033[0m\n"; exit 1; }
