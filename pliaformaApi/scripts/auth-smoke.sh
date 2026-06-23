#!/usr/bin/env bash
# Pliaforma — Auth smoke testi (otomatik test DEĞİL; manuel doğrulamayı kaybetmemek için).
# Tüm auth akışlarını sırayla dener ve beklenen HTTP kodlarını doğrular.
#
# Kullanım:
#   scripts/auth-smoke.sh                      # varsayılan: http://localhost:5132 (backend doğrudan)
#   scripts/auth-smoke.sh http://localhost:3000   # same-origin: frontend proxy üzerinden
#
# Bağımlılık: curl, grep, sed. (jq/python gerekmez.)

set -u
BASE="${1:-http://localhost:5132}"
JAR="$(mktemp)"
EMAIL="smoke+$(date +%s)@pliaforma.local"
PASS_PW="Parola123!"
FAILED=0

cyan() { printf "\033[36m%s\033[0m\n" "$1"; }
ok()   { printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad()  { printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }

# expect <name> <expected-code> <actual-code>
expect() { [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }

json_get() { grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }

cyan "BASE = $BASE"
cyan "EMAIL = $EMAIL"

# 1) register-studio -> 200
RESP=$(curl -s -w "\n%{http_code}" -c "$JAR" -X POST "$BASE/api/auth/register-studio" \
  -H 'Content-Type: application/json' \
  -d "{\"studioName\":\"Smoke Pilates\",\"ownerFullName\":\"Smoke Owner\",\"email\":\"$EMAIL\",\"password\":\"$PASS_PW\"}")
CODE=$(echo "$RESP" | tail -1); BODY=$(echo "$RESP" | sed '$d')
expect "register-studio" 200 "$CODE"
STUDIO_ID=$(echo "$BODY" | json_get studioId)

# 2) login -> 200 (taze cookie)
RESP=$(curl -s -w "\n%{http_code}" -c "$JAR" -X POST "$BASE/api/auth/login" \
  -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS_PW\"}")
CODE=$(echo "$RESP" | tail -1); BODY=$(echo "$RESP" | sed '$d')
expect "login" 200 "$CODE"
AT=$(echo "$BODY" | json_get accessToken)

# 3) me -> 200
CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/auth/me" -H "Authorization: Bearer $AT")
expect "me" 200 "$CODE"

# 4) switch-studio (kendi stüdyosu) -> 200
CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/switch-studio" \
  -H "Authorization: Bearer $AT" -H 'Content-Type: application/json' -d "{\"studioId\":\"$STUDIO_ID\"}")
expect "switch-studio (geçerli)" 200 "$CODE"

# 5) refresh (cookie) -> 200
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$JAR" -c "$JAR" -X POST "$BASE/api/auth/refresh" \
  -H 'Content-Type: application/json' -d "{\"studioId\":\"$STUDIO_ID\"}")
expect "refresh" 200 "$CODE"

# 6) yanlış stüdyoya switch -> 403
CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/switch-studio" \
  -H "Authorization: Bearer $AT" -H 'Content-Type: application/json' \
  -d '{"studioId":"00000000-0000-0000-0000-000000000001"}')
expect "switch-studio (yetkisiz)" 403 "$CODE"

# 7) logout -> 200
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$JAR" -c "$JAR" -X POST "$BASE/api/auth/logout")
expect "logout" 200 "$CODE"

# 8) logout sonrası refresh -> 401
CODE=$(curl -s -o /dev/null -w "%{http_code}" -b "$JAR" -X POST "$BASE/api/auth/refresh" \
  -H 'Content-Type: application/json' -d '{}')
expect "logout sonrası refresh" 401 "$CODE"

# 9) çift e-posta -> 409
CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/register" \
  -H 'Content-Type: application/json' -d "{\"fullName\":\"Dup\",\"email\":\"$EMAIL\",\"password\":\"$PASS_PW\"}")
expect "çift e-posta" 409 "$CODE"

# 10) validation (kısa parola + geçersiz e-posta) -> 400
CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/register-studio" \
  -H 'Content-Type: application/json' -d '{"studioName":"X","ownerFullName":"Y","email":"bad","password":"123"}')
expect "validation" 400 "$CODE"

rm -f "$JAR"
echo
[ "$FAILED" = "0" ] && { cyan "TÜM AKIŞLAR GEÇTİ ✅"; exit 0; } || { cyan "BAZI AKIŞLAR BAŞARISIZ ❌"; exit 1; }
