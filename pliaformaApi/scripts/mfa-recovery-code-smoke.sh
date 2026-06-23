#!/usr/bin/env bash
# Pliaforma — Sprint 31-MFA recovery code smoke. Enroll → login challenge → recovery code ile giriş → tek kullanım.
# ÖN KOŞUL: backend Mfa__Enabled=true. Kullanım: scripts/mfa-recovery-code-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
totp(){ node "$DIR/totp.mjs" "$1" "${2:-0}"; }
post(){ curl -sk -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }

EMAIL="mfarec+$TS@pf.local"
AT=$(body "$(post /api/auth/register-studio "{\"studioName\":\"MFA $TS\",\"ownerFullName\":\"O\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget accessToken)
SECRET=$(body "$(post /api/auth/mfa/setup/start "{}" "$AT")" | jget manualKey)
V=$(post /api/auth/mfa/setup/verify "{\"code\":\"$(totp "$SECRET")\"}" "$AT")
# İlk recovery code'u çıkar.
REC=$(body "$V" | grep -oE '[A-Z0-9]{5}-[A-Z0-9]{5}' | head -1)
[ -n "$REC" ] && ok "enroll + recovery code alındı ($REC)" || bad "recovery code yok"

echo "== recovery code ile login =="
CT=$(body "$(post /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget challengeToken)
expect "yanlış recovery → 401" 401 "$(code "$(post /api/auth/mfa/recovery-code/verify "{\"challengeToken\":\"$CT\",\"recoveryCode\":\"ZZZZZ-ZZZZZ\"}")")"
R1=$(post /api/auth/mfa/recovery-code/verify "{\"challengeToken\":\"$CT\",\"recoveryCode\":\"$REC\"}")
expect "doğru recovery → 200" 200 "$(code "$R1")"
body "$R1" | grep -q '"accessToken"' && ok "recovery sonrası tam token" || bad "tam token yok"

echo "== tek kullanımlık (aynı kod tekrar) =="
CT2=$(body "$(post /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget challengeToken)
expect "aynı recovery code tekrar → 401" 401 "$(code "$(post /api/auth/mfa/recovery-code/verify "{\"challengeToken\":\"$CT2\",\"recoveryCode\":\"$REC\"}")")"

echo
[ "$FAILED" = "0" ] && { printf "\033[32mMFA RECOVERY GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
