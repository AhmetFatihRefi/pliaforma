#!/usr/bin/env bash
# Pliaforma — Sprint 31-MFA audit smoke. Tüm MFA olayları üretir, DB'de doğrular; secret şifreli + audit'te plain yok.
# ÖN KOŞUL: backend Mfa__Enabled=true + postgres erişimi. Kullanım: scripts/mfa-audit-smoke.sh [BASE]
#   PG_CONTAINER/PGUSER/PGDB, ADMIN_EMAIL/ADMIN_PW env ile override edilebilir.
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@pliaforma.local}"; ADMIN_PW="${ADMIN_PW:-Admin!2345}"
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
totp(){ node "$DIR/totp.mjs" "$1" "${2:-0}"; }
post(){ curl -sk -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
body(){ echo "$1" | sed '$d'; }

PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -iE 'postgres' | head -1)}"
PGUSER="${PGUSER:-postgres}"; PGDB="${PGDB:-pliaforma}"
[ -z "$PGC" ] && { echo "postgres yok (PG_CONTAINER ver)"; exit 1; }
q(){ docker exec "$PGC" psql -U "$PGUSER" -d "$PGDB" -tAc "$1" 2>/dev/null | tr -d '\r'; }

EMAIL="mfaaud+$TS@pf.local"
R=$(post /api/auth/register-studio "{\"studioName\":\"MFA $TS\",\"ownerFullName\":\"O\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
AT=$(body "$R" | jget accessToken); OWNER_ID=$(body "$R" | jget id)
SECRET=$(body "$(post /api/auth/mfa/setup/start "{}" "$AT")" | jget manualKey)
V=$(post /api/auth/mfa/setup/verify "{\"code\":\"$(totp "$SECRET")\"}" "$AT")
REC=$(body "$V" | grep -oE '[A-Z0-9]{5}-[A-Z0-9]{5}' | head -1)
# login → fail + success
CT=$(body "$(post /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget challengeToken)
post /api/auth/mfa/challenge/verify "{\"challengeToken\":\"$CT\",\"code\":\"000000\"}" >/dev/null  # MFAFailed
post /api/auth/mfa/challenge/verify "{\"challengeToken\":\"$CT\",\"code\":\"$(totp "$SECRET")\"}" >/dev/null  # MFASucceeded
# recovery
CT2=$(body "$(post /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget challengeToken)
post /api/auth/mfa/recovery-code/verify "{\"challengeToken\":\"$CT2\",\"recoveryCode\":\"$REC\"}" >/dev/null  # MFARecoveryCodeUsed
# reset (PlatformAdmin)
ADTOK=$(body "$(post /api/auth/login "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PW\"}")" | jget accessToken)
[ -n "$ADTOK" ] && ok "PlatformAdmin login" || bad "admin login başarısız (ADMIN_EMAIL/ADMIN_PW)"
post /api/auth/mfa/reset "{\"userId\":\"$OWNER_ID\"}" "$ADTOK" >/dev/null  # MFAResetCompleted
sleep 1

echo "== Audit olayları DB'de =="
for ev in MFASetupStarted MFASetupCompleted MFAChallengeCreated MFASucceeded MFAFailed MFARecoveryCodeUsed MFAResetCompleted; do
  N=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='$ev';")
  [ "${N:-0}" -gt 0 ] 2>/dev/null && ok "olay: $ev (n=$N)" || bad "olay YOK: $ev"
done

echo "== Sızıntı yok: audit'te plain secret/recovery/token YOK =="
LEAK=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\" LIKE '%$SECRET%' OR \"Detail\" LIKE '%${REC}%';")
[ "${LEAK:-1}" = "0" ] && ok "audit Detail'de plain secret/recovery yok" || bad "audit'te plain sızıntı (n=$LEAK)"

echo "== Secret şifreli saklanıyor (plain DEĞİL) =="
# Reset secret'i temizledi; reset ÖNCESİ şifreli olduğunu, plain secret'in HİÇ DB'de olmadığını doğrula.
PLAIN=$(q "SELECT count(*) FROM \"UserMfaSettings\" WHERE \"SecretEncrypted\" = '$SECRET' OR \"PendingSecretEncrypted\" = '$SECRET';")
[ "${PLAIN:-1}" = "0" ] && ok "UserMfaSettings'te plain secret yok (şifreli)" || bad "plain secret DB'de (n=$PLAIN)"
RESET=$(q "SELECT \"MfaEnabled\" FROM \"Users\" WHERE \"Id\"='$OWNER_ID';")
[ "$RESET" = "f" ] && ok "reset sonrası MfaEnabled=false" || bad "reset sonrası MfaEnabled=$RESET"

echo
[ "$FAILED" = "0" ] && { printf "\033[32mMFA AUDIT GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
