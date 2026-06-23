#!/usr/bin/env bash
# Pliaforma — Saat dilimi sözleşmesi smoke (backend her zaman Türkiye saati basar):
#  - UTC alanlar (audit/işlem damgaları, örn. payment createdAt) JSON çıktısında Europe/Istanbul'a çevrilir
#    (UTC+3) ve offset/Z TAŞIMAZ → frontend hiç TZ matematiği yapmadan doğru saati gösterir.
#  - Duvar-saati alanlar (closure/ders StartAt) OLDUĞU GİBİ kalır (çift kayma YOK), Z taşımaz.
# Postgres container gerekli. Kullanım: scripts/timezone-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jval(){ echo "$1" | grep -o "\"$2\":\"[^\"]*\"" | head -1 | sed "s/.*:\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sqlq(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }

ATOK=$(post /api/auth/register-studio "{\"studioName\":\"TZS $TS\",\"ownerFullName\":\"O\",\"email\":\"tzs+$TS@pf.local\",\"password\":\"$PW\"}" | jget accessToken)
SID=$(sqlq "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='TZS $TS';")
SP=$(post "/api/studio/students/guest" '{"fullName":"S","source":"Phone"}' "$ATOK" | jget id)
FUT=$(date -d "+5 days" +%Y-%m-%d)

# Sprint 29A: strict payment → ödeme line'a bağlanır. SP'ye sözleşme (→ line) için hoca + ders grubu bootstrap.
login(){ post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}" | jget accessToken; }
post /api/auth/register "{\"fullName\":\"tzi\",\"email\":\"tzi+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
sqlq "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='tzi+$TS@pf.local' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null
MID=$(post /api/studio/memberships/invite "{\"email\":\"tzi+$TS@pf.local\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK" | jget membershipId)
ITOK=$(login "tzi+$TS@pf.local"); post "/api/members/$MID/accept" '{}' "$ITOK" >/dev/null
IPID=$(get "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "tzi+$TS@pf.local" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/')
CTZ=$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK" | jget id)
post "/api/studio/students/$SP/contracts" "{\"classTypeId\":\"$CTZ\",\"instructorProfileId\":\"$IPID\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":10,\"makeupAllowance\":1,\"price\":0,\"isIndividualDefault\":false}" "$ATOK" >/dev/null
SPL=$(get "/api/studio/students/$SP/contract-lines" "$ATOK" | jget id)

echo "--- 1) UTC alan (payment createdAt) → İstanbul (+3), Z yok ---"
PAY=$(post "/api/studio/students/$SP/payments" "{\"amount\":100,\"dueDate\":\"$FUT\",\"studentContractLineId\":\"$SPL\"}" "$ATOK")
PID=$(echo "$PAY" | jget id)
API_CA=$(jval "$PAY" createdAt | cut -c1-16)
DB_UTC=$(sqlq "SELECT to_char(\"CreatedAt\" AT TIME ZONE 'UTC','YYYY-MM-DD\"T\"HH24:MI') FROM \"Payments\" WHERE \"Id\"='$PID';")
DB_TR=$(sqlq "SELECT to_char(\"CreatedAt\" AT TIME ZONE 'Europe/Istanbul','YYYY-MM-DD\"T\"HH24:MI') FROM \"Payments\" WHERE \"Id\"='$PID';")
expect "API createdAt = DB İstanbul saati" "$DB_TR" "$API_CA"
[ "$API_CA" != "$DB_UTC" ] && ok "API createdAt ≠ DB UTC (çevrim yapıldı: $DB_UTC→$API_CA)" || bad "API createdAt hâlâ UTC ($API_CA)"
echo "$PAY" | grep -qE '"createdAt":"[^"]*(Z|\+0)' && bad "createdAt offset/Z taşıyor" || ok "createdAt offset/Z taşımıyor"

echo "--- 2) Duvar-saati alan (closure startAt 09:00) → değişmez, Z yok ---"
post /api/studio/closures "{\"title\":\"T\",\"startAt\":\"${FUT}T09:00:00\",\"endAt\":\"${FUT}T17:00:00\",\"actionType\":\"CancelSessions\"}" "$ATOK" >/dev/null
CL=$(get "/api/studio/closures" "$ATOK")
expect "closure startAt duvar-saati korunur (09:00, kaymaz)" "${FUT}T09:00:00" "$(jval "$CL" startAt)"
expect "closure endAt duvar-saati korunur (17:00, kaymaz)" "${FUT}T17:00:00" "$(jval "$CL" endAt)"
echo "$CL" | grep -qE '"startAt":"[^"]*(Z|\+0)' && bad "closure startAt offset/Z taşıyor" || ok "closure startAt offset/Z taşımıyor"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mTIMEZONE SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mTIMEZONE SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
