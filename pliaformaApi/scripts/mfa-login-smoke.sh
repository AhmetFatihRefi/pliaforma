#!/usr/bin/env bash
# Pliaforma — Sprint 31-MFA login smoke. Enroll → login challenge → yanlış/doğru TOTP → replay reddi.
# ÖN KOŞUL: backend Mfa__Enabled=true. Kullanım: scripts/mfa-login-smoke.sh [BASE]
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

EMAIL="mfalogin+$TS@pf.local"
# --- Enroll ---
AT=$(body "$(post /api/auth/register-studio "{\"studioName\":\"MFA $TS\",\"ownerFullName\":\"O\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget accessToken)
SECRET=$(body "$(post /api/auth/mfa/setup/start "{}" "$AT")" | jget manualKey)
post /api/auth/mfa/setup/verify "{\"code\":\"$(totp "$SECRET")\"}" "$AT" >/dev/null
ok "kurulum (enroll) tamam"

echo "== MFA-aktif kullanıcı login → challenge (tam token YOK) =="
L=$(post /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
expect "login → 200" 200 "$(code "$L")"
body "$L" | grep -q '"mfaChallengeRequired":true' && ok "login: mfaChallengeRequired=true" || bad "challenge yok"
body "$L" | grep -q '"accessToken"' && bad "login full token döndü (MFA bypass!)" || ok "login full token DÖNMEDİ (challenge gerekli)"
CT=$(body "$L" | jget challengeToken)

echo "== challenge/verify =="
expect "yanlış TOTP → 401" 401 "$(code "$(post /api/auth/mfa/challenge/verify "{\"challengeToken\":\"$CT\",\"code\":\"000000\"}")")"
# Enroll mevcut timestep'i tüketti (replay koruması); challenge için BİR SONRAKİ adım kodu (±1 pencerede + > lastUsed).
CODE=$(totp "$SECRET" 1)
C=$(post /api/auth/mfa/challenge/verify "{\"challengeToken\":\"$CT\",\"code\":\"$CODE\"}")
expect "doğru TOTP → 200" 200 "$(code "$C")"
body "$C" | grep -q '"accessToken"' && ok "challenge sonrası tam token döndü" || bad "tam token yok"

echo "== replay reddi (aynı timestep kodu yeniden) =="
L2=$(post /api/auth/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}"); CT2=$(body "$L2" | jget challengeToken)
expect "aynı kod tekrar (replay) → 401" 401 "$(code "$(post /api/auth/mfa/challenge/verify "{\"challengeToken\":\"$CT2\",\"code\":\"$CODE\"}")")"

echo
[ "$FAILED" = "0" ] && { printf "\033[32mMFA LOGIN GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
