#!/usr/bin/env bash
# Pliaforma — Sprint 16 smoke (e-posta doğrulama + şifre sıfırlama). LogEmailSender (Log mode) verification/reset
# linkini backend stdout/log'una yazar; smoke ham token'ı **logdan** çeker (DB'de yalnız hash — geri çevrilemez).
# Log kaynağı ortamdan bağımsız (native dosya | compose/docker container logu):
#   PF_LOG_SOURCE=file    (vars.) → PF_API_LOG dosyasını okur (vars. /c/tmp/pf-api.log)  [native dev]
#   PF_LOG_SOURCE=docker  → PF_LOG_CONTAINER container'ının logunu okur (vars. pliaforma-dev-backend-1)
#   PF_LOG_SOURCE=compose → docker compose -f PF_COMPOSE_FILE logs PF_COMPOSE_SERVICE (vars. docker-compose.dev.yml / backend)
# Kullanım: scripts/sprint16-smoke.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
LOG_SOURCE="${PF_LOG_SOURCE:-file}"
LOG_FILE="${PF_API_LOG:-/c/tmp/pf-api.log}"
LOG_CONTAINER="${PF_LOG_CONTAINER:-pliaforma-dev-backend-1}"
COMPOSE_FILE="${PF_COMPOSE_FILE:-docker-compose.dev.yml}"
COMPOSE_SERVICE="${PF_COMPOSE_SERVICE:-backend}"
TS=$(date +%s); PW="Parola123"; NEWPW="YeniParola456"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
uid(){ sql "SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='$1';"; }
# Log kaynağını (dosya | docker | compose) ortamdan bağımsız okur.
readlog(){
  case "$LOG_SOURCE" in
    docker)  docker logs "$LOG_CONTAINER" 2>&1 ;;
    compose) docker compose -f "$COMPOSE_FILE" logs --no-color "$COMPOSE_SERVICE" 2>&1 ;;
    *)       cat "$LOG_FILE" 2>/dev/null ;;
  esac
}
# Log kaynağı erişilebilir mi? (token bu kaynaktan çekilir — DB'den ham token ÇIKARILMAZ.)
case "$LOG_SOURCE" in
  file)    [ -f "$LOG_FILE" ] || { echo "API log dosyası yok: $LOG_FILE (PF_API_LOG ile override edin)."; exit 2; } ;;
  docker)  docker logs "$LOG_CONTAINER" >/dev/null 2>&1 || { echo "Backend container logu okunamadı: $LOG_CONTAINER."; exit 2; } ;;
  compose) docker compose -f "$COMPOSE_FILE" logs "$COMPOSE_SERVICE" >/dev/null 2>&1 || { echo "Compose logu okunamadı: $COMPOSE_FILE / $COMPOSE_SERVICE."; exit 2; } ;;
esac
vtoken(){ sleep 0.8; readlog | grep "EmailVerification → $1 " | tail -1 | sed -n 's/.*token=\([^ ]*\).*/\1/p'; }
rtoken(){ sleep 0.8; readlog | grep "PasswordReset → $1 " | tail -1 | sed -n 's/.*token=\([^ ]*\).*/\1/p'; }
reg(){ post /api/auth/register "{\"fullName\":\"U\",\"email\":\"$1\",\"password\":\"$PW\"}"; }
verifyc(){ pcode /api/auth/verify-email "{\"token\":\"$1\"}"; }

echo "--- 1) Yeni register unverified + token DB'de hash'li + log ---"
E1="s16a+$TS@pf.local"; R1=$(reg "$E1"); expect "register → 200" 200 "$(code "$R1")"
expect "yeni kullanıcı emailVerified=false" yes "$(has "$(body "$R1")" '"emailVerified":false')"
U1=$(uid "$E1"); U1TOK=$(body "$R1" | jget accessToken)
expect "EmailVerification token DB'de var (hash)" 1 "$(sql "SELECT count(*) FROM \"AuthTokens\" WHERE \"UserId\"='$U1' AND \"Purpose\"='EmailVerification' AND length(\"TokenHash\")>0;")"
VT1=$(vtoken "$E1")
expect "ham token log'a yazıldı (LogEmailSender)" yes "$([ -n "$VT1" ] && echo yes || echo no)"
expect "DB'de ham token TUTULMUYOR (hash != ham)" 0 "$(sql "SELECT count(*) FROM \"AuthTokens\" WHERE \"TokenHash\"='$VT1';")"

echo "--- 2) Unverified apply artık PENDING oluşturur (Rev2: e-posta gate'i apply'da DEĞİL, aktivasyonda) ---"
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S16 $TS\",\"ownerFullName\":\"O\",\"email\":\"s16owner+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
JCA=$(curl -s "$BASE/api/studio/join-code" -H "Authorization: Bearer $ATOK" | jget joinCode)
RAP=$(post /api/members/apply "{\"joinCode\":\"$JCA\",\"role\":\"Student\"}" "$U1TOK")
expect "unverified kullanıcı apply → 200 (Pending)" 200 "$(code "$RAP")"
MID=$(body "$RAP" | jget membershipId)
expect "unverified applicant'ı owner accept → 403 (aktivasyon gate'i)" 403 "$(pcode "/api/members/$MID/accept" '{}' "$ATOK")"

echo "--- 3) Verify başarılı + ikinci kez geçersiz ---"
expect "verify token → 200" 200 "$(verifyc "$VT1")"
expect "verify sonrası login'de emailVerified=true" yes "$(has "$(body "$(post /api/auth/login "{\"email\":\"$E1\",\"password\":\"$PW\"}")")" '"emailVerified":true')"
expect "aynı token ikinci kez → 409" 409 "$(verifyc "$VT1")"

echo "--- 4) Doğrulama sonrası owner aktive edebilir (aktivasyon gate'i geçer) ---"
expect "verified applicant'ı owner accept → 200" 200 "$(pcode "/api/members/$MID/accept" '{}' "$ATOK")"

echo "--- 5) Expired token geçersiz ---"
E2="s16exp+$TS@pf.local"; R2=$(reg "$E2"); U2=$(uid "$E2"); VT2=$(vtoken "$E2")
sql "UPDATE \"AuthTokens\" SET \"ExpiresAt\"='2020-01-01' WHERE \"UserId\"='$U2' AND \"Purpose\"='EmailVerification' AND \"ConsumedAt\" IS NULL;" >/dev/null
expect "expired token verify → 409" 409 "$(verifyc "$VT2")"

echo "--- 6) Resend: verified kullanıcıda yeni token YOK + cooldown generic 200 ---"
# E1 verified; resend → 200 ama yeni token üretmez (EmailVerification token sayısı sabit)
BEFORE=$(sql "SELECT count(*) FROM \"AuthTokens\" WHERE \"UserId\"='$U1' AND \"Purpose\"='EmailVerification';")
expect "verified resend → 200" 200 "$(pcode /api/auth/resend-verification-email '{}' "$U1TOK")"
expect "verified resend yeni token üretmedi" "$BEFORE" "$(sql "SELECT count(*) FROM \"AuthTokens\" WHERE \"UserId\"='$U1' AND \"Purpose\"='EmailVerification';")"
# E3 fresh unverified: ilk token t0'da; hemen resend → cooldown → 200, yeni token yok (sayı sabit=1)
E3="s16cd+$TS@pf.local"; R3=$(reg "$E3"); U3=$(uid "$E3"); U3TOK=$(body "$R3" | jget accessToken)
expect "cooldown içinde resend → 200 (generic)" 200 "$(pcode /api/auth/resend-verification-email '{}' "$U3TOK")"
expect "cooldown: yeni token üretilmedi (1)" 1 "$(sql "SELECT count(*) FROM \"AuthTokens\" WHERE \"UserId\"='$U3' AND \"Purpose\"='EmailVerification';")"

echo "--- 7) Forgot-password generic 200 (var/yok sızdırmaz) ---"
expect "forgot (mevcut email) → 200" 200 "$(pcode /api/auth/forgot-password "{\"email\":\"$E1\"}")"
expect "forgot (olmayan email) → 200" 200 "$(pcode /api/auth/forgot-password "{\"email\":\"yok-$TS@pf.local\"}")"

echo "--- 8) Reset password + refresh revoke + reset email'i doğrular ---"
E4="s16reset+$TS@pf.local"; R4=$(reg "$E4"); U4=$(uid "$E4")
# login (cookie jar) → eski refresh çalışır
JAR="/tmp/pf16jar_$TS"; curl -s -c "$JAR" -o /dev/null -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$E4\",\"password\":\"$PW\"}"
expect "reset öncesi refresh → 200" 200 "$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -d '{}')"
# forgot → reset token logdan
pcode /api/auth/forgot-password "{\"email\":\"$E4\"}" >/dev/null
RT=$(rtoken "$E4")
expect "reset token → 200" 200 "$(pcode /api/auth/reset-password "{\"token\":\"$RT\",\"newPassword\":\"$NEWPW\"}")"
expect "eski şifre → 401" 401 "$(pcode /api/auth/login "{\"email\":\"$E4\",\"password\":\"$PW\"}")"
expect "yeni şifre → 200" 200 "$(pcode /api/auth/login "{\"email\":\"$E4\",\"password\":\"$NEWPW\"}")"
expect "reset token ikinci kez → 409" 409 "$(pcode /api/auth/reset-password "{\"token\":\"$RT\",\"newPassword\":\"x9y8z7w6\"}")"
expect "reset sonrası eski refresh → 401 (revoke)" 401 "$(curl -s -b "$JAR" -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -d '{}')"
expect "reset, EmailVerifiedAt null ise doğruladı (login emailVerified=true)" yes "$(has "$(body "$(post /api/auth/login "{\"email\":\"$E4\",\"password\":\"$NEWPW\"}")")" '"emailVerified":true')"
rm -f "$JAR"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 16 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 16 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
