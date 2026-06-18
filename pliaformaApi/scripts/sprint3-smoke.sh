#!/usr/bin/env bash
# Pliaforma — Sprint 3 smoke (müsaitlik + ders seansı + EndAt + çakışma + müsaitlik validasyonu +
# bulk-generate + instructor scope + tenant izolasyonu + student engeli).
# Kullanım: scripts/sprint3-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jgeti(){ grep -o "\"$1\":[0-9]*" | head -1 | sed "s/\"$1\)\?\"://;s/\"$1\"://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
put(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

DAY=$(LC_ALL=C date -d "+3 days" +%A); DATE=$(date -d "+3 days" +%Y-%m-%d)
OWNA="s3a+$TS@pf.local"; OWNC="s3c+$TS@pf.local"; I1="s3i1+$TS@pf.local"; I2="s3i2+$TS@pf.local"; STU="s3stu+$TS@pf.local"
echo "Gün=$DAY Tarih=$DATE"

# Aktörler
R=$(post /api/auth/register-studio "{\"studioName\":\"S3A $TS\",\"ownerFullName\":\"O A\",\"email\":\"$OWNA\",\"password\":\"$PW\"}")
ATOK=$(body "$R" | jget accessToken); SID=$(body "$R" | jget studioId)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S3C $TS\",\"ownerFullName\":\"O C\",\"email\":\"$OWNC\",\"password\":\"$PW\"}")" | jget accessToken)
for E in "$I1" "$I2" "$STU"; do post /api/auth/register "{\"fullName\":\"U\",\"email\":\"$E\",\"password\":\"$PW\"}" >/dev/null; done
STU_ID=$(body "$(post /api/auth/login "{\"email\":\"$STU\",\"password\":\"$PW\"}")" | jget id) # not used; id from register needed
STU_ID=$(docker exec "$PGC" psql -U postgres -d pliaforma -t -A -c "select \"Id\" from \"Users\" where \"Email\"='$STU';" 2>/dev/null | tr -d '[:space:]')
# Hocaları davet + kabul + profilleri
declare -A PROF
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
for E in "$I1" "$I2"; do
  docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$E' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1
  RR=$(post /api/studio/memberships/invite "{\"email\":\"$E\",\"role\":\"Instructor\",\"rate\":100}" "$ATOK")
  M=$(body "$RR" | jget membershipId)
  T=$(login "$E"); post "/api/members/$M/accept" '{}' "$T" >/dev/null
  PROF[$E]=$(ipid "$E")
done
I1TOK=$(login "$I1"); I2TOK=$(login "$I2"); I1P=${PROF[$I1]}; I2P=${PROF[$I2]}
# ClassType (süre 60, kapasite 6)
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)

echo "--- Müsaitlik ---"
R=$(post "/api/studio/instructors/$I1P/availabilities" "{\"dayOfWeek\":\"$DAY\",\"startTime\":\"09:00:00\",\"endTime\":\"12:00:00\"}" "$ATOK")
expect "Owner create availability (I1)" 200 "$(code "$R")"; AV1=$(body "$R" | jget id)
expect "overlap availability → 409" 409 "$(code "$(post "/api/studio/instructors/$I1P/availabilities" "{\"dayOfWeek\":\"$DAY\",\"startTime\":\"11:00:00\",\"endTime\":\"13:00:00\"}" "$ATOK")")"
expect "Instructor1 create OWN availability" 200 "$(code "$(post "/api/studio/instructors/$I1P/availabilities" "{\"dayOfWeek\":\"Monday\",\"startTime\":\"09:00:00\",\"endTime\":\"10:00:00\"}" "$I1TOK")")"
expect "Instructor2 başka hocanın availability → 403" 403 "$(code "$(post "/api/studio/instructors/$I1P/availabilities" "{\"dayOfWeek\":\"Tuesday\",\"startTime\":\"09:00:00\",\"endTime\":\"10:00:00\"}" "$I2TOK")")"

echo "--- Ders seansı + EndAt + çakışma + müsaitlik ---"
R=$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${DATE}T10:00:00\"}" "$ATOK")
expect "create session (avail içinde)" 200 "$(code "$R")"; SESS=$(body "$R" | jget id)
expect "EndAt = start+60 (11:00)" yes "$(has "$(body "$R")" "${DATE}T11:00:00")"
expect "capacity default=ClassType.MaxCapacity(6)" yes "$(has "$(body "$R")" '"capacity":6')"
expect "çakışan session → 409" 409 "$(code "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${DATE}T10:30:00\"}" "$ATOK")")"
expect "müsaitlik dışı session → 409" 409 "$(code "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${DATE}T13:00:00\"}" "$ATOK")")"
expect "geçmiş tarihe session → 409" 409 "$(code "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"2020-01-01T10:00:00\"}" "$ATOK")")"
expect "Instructor (müsaitliksiz) kendi session → 409" 409 "$(code "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CT\",\"startAt\":\"${DATE}T10:00:00\"}" "$I2TOK")")"

echo "--- Bulk generate (09-12, 60dk → 09/10/11; 10 çakışır) ---"
R=$(post /api/studio/class-sessions/bulk-generate "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"date\":\"$DATE\",\"rangeStart\":\"09:00:00\",\"rangeEnd\":\"12:00:00\"}" "$ATOK")
expect "bulk-generate 200" 200 "$(code "$R")"
expect "bulk created 09:00" yes "$(has "$(body "$R")" "${DATE}T09:00:00")"
expect "bulk skipped conflict (10:00)" yes "$(has "$(body "$R")" 'conflict')"

echo "--- Instructor scope ---"
expect "Instructor2 başkasının session get → 403" 403 "$(gcode "/api/studio/class-sessions/$SESS" "$I2TOK")"
expect "Instructor2 başkasının session cancel → 403" 403 "$(code "$(post "/api/studio/class-sessions/$SESS/cancel" '{}' "$I2TOK")")"

echo "--- Tenant izolasyonu (Owner C) ---"
expect "C get A session → 404" 404 "$(gcode "/api/studio/class-sessions/$SESS" "$CTOK")"
expect "C cancel A session → 404" 404 "$(code "$(post "/api/studio/class-sessions/$SESS/cancel" '{}' "$CTOK")")"
expect "C update A availability → 404" 404 "$(put "/api/studio/instructor-availabilities/$AV1" "{\"dayOfWeek\":\"$DAY\",\"startTime\":\"08:00:00\",\"endTime\":\"09:00:00\",\"isActive\":true}" "$CTOK")"
expect "C delete A availability → 404" 404 "$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/studio/instructor-availabilities/$AV1" -H "Authorization: Bearer $CTOK")"

echo "--- Student erişim engeli (üyelik DB arrange) ---"
SQL="INSERT INTO \"StudioMemberships\" (\"Id\",\"StudioId\",\"UserId\",\"Role\",\"Status\",\"Direction\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$SID','$STU_ID','Student','Active','InvitedByStudio', now() at time zone 'utc', false);"
if docker exec "$PGC" psql -U postgres -d pliaforma -c "$SQL" >/dev/null 2>&1; then
  STOK=$(login "$STU")
  expect "Student session listesine → 403" 403 "$(gcode /api/studio/class-sessions "$STOK")"
  expect "Student availability listesine → 403" 403 "$(gcode /api/studio/instructor-availabilities "$STOK")"
else
  bad "Student arrange (DB insert) başarısız"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 3 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 3 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
