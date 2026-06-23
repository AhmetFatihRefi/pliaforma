#!/usr/bin/env bash
# Pliaforma — Sprint 31-MFA setup smoke. Owner setup-pending → enforcement → setup/start → verify → full token.
# ÖN KOŞUL: backend Mfa__Enabled=true ile çalışıyor. Kullanım: scripts/mfa-setup-smoke.sh [BASE]
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
pcode(){ curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -sk -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }

EMAIL="mfaset+$TS@pf.local"
R=$(post /api/auth/register-studio "{\"studioName\":\"MFA $TS\",\"ownerFullName\":\"O\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
expect "register-studio → 200" 200 "$(code "$R")"
AT=$(body "$R" | jget accessToken)
body "$R" | grep -q '"mfaSetupRequired":true' && ok "register-studio: mfaSetupRequired=true" || bad "mfaSetupRequired yok"

echo "== mfa_pending enforcement (API düzeyi) =="
expect "mfa_pending → /api/studio 403" 403 "$(gcode /api/studio "$AT")"
expect "mfa_pending → /api/auth/me 200 (izinli)" 200 "$(gcode /api/auth/me "$AT")"

echo "== setup/start + verify =="
S=$(post /api/auth/mfa/setup/start "{}" "$AT")
expect "setup/start → 200" 200 "$(code "$S")"
SECRET=$(body "$S" | jget manualKey)
[ -n "$SECRET" ] && ok "secret + otpauth alındı" || bad "secret yok"
body "$S" | grep -q 'otpauth://totp' && ok "otpauthUri döndü" || bad "otpauthUri yok"

expect "setup/verify yanlış kod → 400" 400 "$(pcode /api/auth/mfa/setup/verify "{\"code\":\"000000\"}" "$AT")"

V=$(post /api/auth/mfa/setup/verify "{\"code\":\"$(totp "$SECRET")\"}" "$AT")
expect "setup/verify doğru kod → 200" 200 "$(code "$V")"
body "$V" | grep -qE '"recoveryCodes":\[("[A-Z0-9]{5}-[A-Z0-9]{5}"){1}' && ok "recovery code'lar döndü (bir kez)" || bad "recovery code yok"
NEWAT=$(body "$V" | jget accessToken)

echo "== MFA sonrası tam erişim =="
expect "yeni token → /api/studio 200 (full)" 200 "$(gcode /api/studio "$NEWAT")"

echo
[ "$FAILED" = "0" ] && { printf "\033[32mMFA SETUP GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
