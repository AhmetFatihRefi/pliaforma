#!/usr/bin/env bash
# Pliaforma — Sprint 31B-2 change-password smoke.
# Yanlış mevcut parola → 400; doğru mevcut + güçlü yeni → 200; sonra eski refresh revoke (401); yeni parola login → 200.
# Kullanım: scripts/change-password-smoke.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; NEW="YeniParola456!"; FAILED=0
JAR="$(mktemp)"
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
chg(){ curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/change-password" \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $1" -d "$2"; }

EMAIL="chg+$TS@pf.local"
# Kayıt: accessToken (gövde) + refresh cookie (JAR).
BODY=$(curl -sk -c "$JAR" -X POST "$BASE/api/auth/register-studio" -H 'Content-Type: application/json' \
  -d "{\"studioName\":\"Chg $TS\",\"ownerFullName\":\"O\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
AT=$(echo "$BODY" | jget accessToken)
[ -n "$AT" ] && ok "kayıt + accessToken alındı" || bad "kayıt/accessToken yok"

expect "yanlış mevcut parola → 400" 400 "$(chg "$AT" "{\"currentPassword\":\"YanlisXX123!\",\"newPassword\":\"$NEW\"}")"
expect "zayıf yeni parola → 400"    400 "$(chg "$AT" "{\"currentPassword\":\"$PW\",\"newPassword\":\"zayif\"}")"
expect "doğru mevcut + güçlü yeni → 200" 200 "$(chg "$AT" "{\"currentPassword\":\"$PW\",\"newPassword\":\"$NEW\"}")"

# Eski refresh cookie (JAR) artık revoke olmalı → /refresh 401.
RC=$(curl -sk -o /dev/null -w "%{http_code}" -b "$JAR" -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -d '{}')
expect "değişim sonrası eski refresh → 401" 401 "$RC"

# Eski parola artık geçmez, yeni parola geçer.
expect "eski parola ile login → 401" 401 "$(curl -sk -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")"
expect "yeni parola ile login → 200" 200 "$(curl -sk -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\",\"password\":\"$NEW\"}")"

rm -f "$JAR"
echo
[ "$FAILED" = "0" ] && { printf "\033[32mCHANGE-PASSWORD GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
