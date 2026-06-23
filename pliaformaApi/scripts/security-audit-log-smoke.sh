#!/usr/bin/env bash
# Pliaforma — Sprint 31B-2 SecurityAuditLog smoke.
# Kimlik olayları üretir, ardından DB'de denetim kayıtlarını doğrular: olaylar yazıldı, e-posta MASKELİ,
# ham parola/token kolonu YOK, tablo append-only (soft-delete kolonu yok).
# Kullanım: scripts/security-audit-log-smoke.sh [BASE]   (vars. http://localhost:5132)
#   PG_CONTAINER, PGUSER, PGDB env ile override edilebilir (vars. otomatik tespit / pliaforma / pliaforma).
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; WRONG="Yanlis123!"; FAILED=0
JAR="$(mktemp)"
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }

PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -iE 'postgres' | head -1)}"
PGUSER="${PGUSER:-pliaforma}"; PGDB="${PGDB:-pliaforma}"
if [ -z "$PGC" ]; then printf "  \033[31mFAIL\033[0m postgres konteyneri bulunamadı (PG_CONTAINER ver)\n"; exit 1; fi
q(){ docker exec "$PGC" psql -U "$PGUSER" -d "$PGDB" -tAc "$1" 2>/dev/null | tr -d '\r'; }

EMAIL="audit$TS@pf.local"
echo "== Kimlik olayları üret (email: $EMAIL) =="
BODY=$(curl -sk -c "$JAR" -X POST "$BASE/api/auth/register-studio" -H 'Content-Type: application/json' \
  -d "{\"studioName\":\"Audit $TS\",\"ownerFullName\":\"O\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
AT=$(echo "$BODY" | jget accessToken)
JAR2="$(mktemp)"
curl -sk -o /dev/null -c "$JAR2" -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}"   # LoginSucceeded (+taze cookie)
curl -sk -o /dev/null -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\",\"password\":\"$WRONG\"}"   # LoginFailed
curl -sk -o /dev/null -b "$JAR2" -X POST "$BASE/api/auth/logout" -H 'Content-Type: application/json' -d '{}'                                   # Logout
curl -sk -o /dev/null -X POST "$BASE/api/auth/forgot-password" -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\"}"                  # PasswordResetRequested
curl -sk -o /dev/null -X POST "$BASE/api/auth/change-password" -H 'Content-Type: application/json' -H "Authorization: Bearer $AT" \
  -d "{\"currentPassword\":\"$PW\",\"newPassword\":\"YeniParola456!\"}"                                                                        # PasswordChanged
sleep 1

echo "== DB doğrulaması (SecurityAuditLogs) =="
MASK=$(q "SELECT \"ActorEmailMasked\" FROM \"SecurityAuditLogs\" WHERE \"ActorEmailMasked\" IS NOT NULL ORDER BY \"OccurredAt\" DESC LIMIT 1;")
TOTAL=$(q "SELECT count(*) FROM \"SecurityAuditLogs\";")
[ "${TOTAL:-0}" -gt 0 ] 2>/dev/null && ok "denetim kaydı üretildi (toplam=$TOTAL)" || bad "denetim kaydı yok"

for ev in LoginSucceeded LoginFailed Logout PasswordResetRequested PasswordChanged; do
  N=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='$ev';")
  [ "${N:-0}" -gt 0 ] 2>/dev/null && ok "olay yazıldı: $ev (n=$N)" || bad "olay YOK: $ev"
done

# E-posta maskeli mi? (örnek bir maskeli değer '***' içermeli; ham 'audit$TS' İÇERMEMELİ)
echo "$MASK" | grep -q '\*\*\*' && ok "ActorEmailMasked maskeli ('$MASK')" || bad "ActorEmailMasked maskeli değil ('$MASK')"
RAW=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"ActorEmailMasked\" LIKE '%audit$TS%';")
[ "${RAW:-0}" = "0" ] && ok "ham e-posta saklanmıyor" || bad "ham e-posta sızmış (n=$RAW)"

# Şema: parola/token/secret kolonu YOK; soft-delete (IsDeleted) kolonu YOK → append-only/BaseEntity değil.
BADCOLS=$(q "SELECT count(*) FROM information_schema.columns WHERE table_name='SecurityAuditLogs' AND (lower(column_name) LIKE '%password%' OR lower(column_name) LIKE '%token%' OR lower(column_name) LIKE '%secret%');")
[ "${BADCOLS:-1}" = "0" ] && ok "parola/token/secret kolonu yok" || bad "hassas kolon var (n=$BADCOLS)"
SOFTDEL=$(q "SELECT count(*) FROM information_schema.columns WHERE table_name='SecurityAuditLogs' AND column_name IN ('IsDeleted','UpdatedAt');")
[ "${SOFTDEL:-1}" = "0" ] && ok "soft-delete/UpdatedAt kolonu yok (append-only, BaseEntity değil)" || bad "soft-delete kolonu var (n=$SOFTDEL)"

rm -f "$JAR" "$JAR2"
echo
[ "$FAILED" = "0" ] && { printf "\033[32mSECURITY AUDIT LOG GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
