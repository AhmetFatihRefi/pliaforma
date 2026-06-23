#!/usr/bin/env bash
# Pliaforma — Sprint 31D-2 backup encryption smoke.
# gpg + şifreli backup + wrong/correct key + restore-drill + eski .sql.gz geri uyum + prod fail-fast + retention.
# Kullanım: PG_CONTAINER=pliaforma-dev-postgres-1 POSTGRES_DB=pliaforma POSTGRES_USER=postgres scripts/backup-encryption-smoke.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OPS="$ROOT/ops"
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
DB="${POSTGRES_DB:-pliaforma}"; USER="${POSTGRES_USER:-postgres}"
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
[ -z "$PGC" ] && { echo "postgres yok (PG_CONTAINER ver)." >&2; exit 1; }
TMP="$(mktemp -d)"; KEY="$(openssl rand -base64 32)"; T1="be_plain_$(date +%s)"
cleanup(){ docker exec "$PGC" psql -U "$USER" -c "DROP DATABASE IF EXISTS \"$T1\";" >/dev/null 2>&1; rm -rf "$TMP"; }
trap cleanup EXIT

echo "== gpg mevcut =="
command -v gpg >/dev/null && ok "gpg kurulu ($(gpg --version | head -1))" || bad "gpg yok"

echo "== Prod fail-fast (REQUIRE_BACKUP_ENCRYPTION=1, key yok) =="
if REQUIRE_BACKUP_ENCRYPTION=1 PG_CONTAINER="$PGC" POSTGRES_DB="$DB" POSTGRES_USER="$USER" RETENTION_DAYS=0 bash "$OPS/backup-postgres.sh" "$TMP" >/dev/null 2>&1; then bad "prod'da key'siz backup ÜRETİLDİ (fail-fast yok)"; else ok "prod/require + key yok → fail-fast"; fi
[ -z "$(ls -1 "$TMP" 2>/dev/null)" ] && ok "fail-fast sonrası dosya üretilmedi" || bad "fail-fast'e rağmen dosya var"

echo "== Dev plain backup (key yok → uyarı + .sql.gz) =="
PG_CONTAINER="$PGC" POSTGRES_DB="$DB" POSTGRES_USER="$USER" RETENTION_DAYS=0 bash "$OPS/backup-postgres.sh" "$TMP" >/dev/null 2>&1
PLAIN="$(ls -1t "$TMP"/*.sql.gz 2>/dev/null | grep -v '\.gpg$' | head -1)"
[ -n "$PLAIN" ] && ok "dev plain .sql.gz üretildi" || bad "dev plain backup üretilmedi"
gunzip -t "$PLAIN" 2>/dev/null && ok "plain dosya geçerli gzip" || bad "plain dosya gzip değil"

echo "== Backward compat: eski plain .sql.gz restore =="
if PG_CONTAINER="$PGC" POSTGRES_USER="$USER" bash "$OPS/restore-postgres.sh" "$PLAIN" "$T1" >/dev/null 2>&1; then ok "plain .sql.gz restore çalışıyor (geri uyum)"; else bad "plain .sql.gz restore bozuldu"; fi
UC=$(docker exec "$PGC" psql -U "$USER" -d "$T1" -tAc "SELECT count(*) FROM \"Users\";" 2>/dev/null | tr -d '\r')
case "$UC" in (''|*[!0-9]*) bad "plain restore tablo sayımı başarısız";; (*) ok "plain restore Users sayıldı (n=$UC)";; esac

echo "== Encrypted backup + plaintext yok =="
ENC="$TMP/enc_test.sql.gz.gpg"
docker exec "$PGC" pg_dump -U "$USER" --clean --if-exists "$DB" | gzip | gpg --symmetric --cipher-algo AES256 --batch --yes --pinentry-mode loopback --passphrase-fd 3 -o "$ENC" 3< <(printf '%s' "$KEY") 2>/dev/null
[ -f "$ENC" ] && ok ".sql.gz.gpg üretildi" || bad "şifreli dosya üretilmedi"
gunzip -t "$ENC" 2>/dev/null && bad "şifreli dosya düz gzip (olmamalı)" || ok "düz gzip değil (şifreli)"
grep -qaiE "CREATE TABLE|INSERT INTO" "$ENC" 2>/dev/null && bad "şifreli dosyada plain SQL var!" || ok "şifreli dosyada plain SQL yok"

echo "== Wrong key fail / correct key success =="
gpg --batch --yes --pinentry-mode loopback --passphrase-fd 3 -d "$ENC" 3< <(printf '%s' "nope-$KEY") >/dev/null 2>&1 && bad "yanlış key decrypt etti" || ok "yanlış key decrypt edemedi"
# grep -c (tümünü okur) → erken-çıkış SIGPIPE'ı yok (pipefail ile uyumlu).
DEC=$(gpg --batch --yes --pinentry-mode loopback --passphrase-fd 3 -d "$ENC" 3< <(printf '%s' "$KEY") 2>/dev/null | gunzip 2>/dev/null | grep -ciE "CREATE TABLE|SET ")
[ "${DEC:-0}" -gt 0 ] && ok "doğru key decrypt etti" || bad "doğru key decrypt edemedi"

echo "== Retention pattern (.sql.gz + .sql.gz.gpg kapsar) =="
ls -1 "$TMP"/*.sql.gz* 2>/dev/null | grep -q '\.sql\.gz\.gpg$' && ls -1 "$TMP"/*.sql.gz* 2>/dev/null | grep -qv '\.gpg$' && ok "listeleme hem .sql.gz hem .sql.gz.gpg görüyor" || bad "retention pattern bir türü kaçırıyor"

echo "== Gerçek key repo'ya girmedi =="
grep -q "CHANGE_ME" "$ROOT/deploy/.env.example" 2>/dev/null && grep -qi "BACKUP_ENCRYPTION_KEY" "$ROOT/deploy/.env.example" 2>/dev/null && ok ".env.example placeholder (gerçek key yok)" || bad ".env.example BACKUP_ENCRYPTION_KEY placeholder eksik"

echo "== Restore drill (uçtan uca) =="
if PG_CONTAINER="$PGC" POSTGRES_DB="$DB" POSTGRES_USER="$USER" bash "$OPS/restore-drill.sh" >/dev/null 2>&1; then ok "restore-drill geçti"; else bad "restore-drill başarısız"; fi

echo
[ "$FAILED" = "0" ] && { printf "\033[36mBACKUP ENCRYPTION: TÜM KONTROLLER GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mBACKUP ENCRYPTION: BAŞARISIZ ❌\033[0m\n"; exit 1; }
