#!/usr/bin/env bash
# Pliaforma — Sprint 29C smoke (Delivered Consumption + Unconfirm + Planned/Delivered/Remaining):
#  - booking → planned+1 (remaining sabit, available-1); confirm → delivered+1/planned-1/remaining-1 (available sabit);
#    unconfirm → tersi. Completed seansta cancel → 409; unconfirm sonrası scheduled cancel → 200.
#  - over-planning guard AvailableLessonCount ile; Absent Completed→delivered; makeup booking regular sayımı değiştirmez.
#  - legacy tek-line null-line Planned'a sayılır; çok-line header null-line sayılmaz.
# Postgres + node gerekli. Kullanım: scripts/sprint29c-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }

echo "== Sprint 29C: Delivered Consumption + Unconfirm + Planned/Delivered/Remaining =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S29C $TS\",\"ownerFullName\":\"O\",\"email\":\"o29c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
e="i1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"I1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; I1P=$(ipid "$e")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
TODAY=$(date +%F); END=$(date -d "+45 days" +%F); FUT=$(date -d "+3 days" +%Y-%m-%dT10:00:00)
SLOT='[{"dayOfWeek":"Monday","preferredStartTime":null}]'
# addline student lessonCount → line id
addline(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$2,\"pricePerLesson\":100,\"makeupAllowance\":2,\"isIndividual\":false,\"currency\":\"TRY\",\"preferredSlots\":$SLOT}" "$ATOK")" | jget id; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$1\"${2:+,\"sessionKind\":\"$2\"}}" "$ATOK")" | jget id; }
bookid(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }
bookcode(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK"; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='2024-01-01 10:00:00', \"EndAt\"='2024-01-01 11:00:00' WHERE \"Id\"='$1';" >/dev/null; }
confirm(){ pcode "/api/studio/class-sessions/$1/confirm" '{}' "$ATOK"; }
unconfirm(){ pcode "/api/studio/class-sessions/$1/unconfirm" '{}' "$ATOK"; }
cancelb(){ pcode "/api/studio/bookings/$1/cancel" '{}' "$ATOK"; }
lc(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.$3"; }
hdrof(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.studentContractId"; }

echo "--- 1) booking → planned+1 (remaining sabit, available-1) ---"
SA=$(guest SA); LA=$(addline "$SA" 10)
SES=$(mksess "$FUT"); BK=$(bookid "$SES" "$SA")
expectn "planned=1" 1 "$(lc "$SA" "$LA" plannedLessonCount)"
expectn "delivered=0" 0 "$(lc "$SA" "$LA" deliveredLessonCount)"
expectn "remainingLessonRight=10 (değişmez)" 10 "$(lc "$SA" "$LA" remainingLessonRight)"
expectn "availableLessonCount=9 (-1)" 9 "$(lc "$SA" "$LA" availableLessonCount)"
expectn "scheduledLessonCount=1 (reserved)" 1 "$(lc "$SA" "$LA" scheduledLessonCount)"

echo "--- 2) confirm → delivered+1/planned-1/remaining-1 (available sabit) ---"
backdate "$SES"; expect "confirm → 200" 200 "$(confirm "$SES")"
expectn "delivered=1" 1 "$(lc "$SA" "$LA" deliveredLessonCount)"
expectn "planned=0" 0 "$(lc "$SA" "$LA" plannedLessonCount)"
expectn "remainingLessonRight=9 (-1)" 9 "$(lc "$SA" "$LA" remainingLessonRight)"
expectn "availableLessonCount=9 (aynı; reserved kapısı korunur)" 9 "$(lc "$SA" "$LA" availableLessonCount)"

echo "--- 3) Completed seansta booking cancel → 409 ---"
expect "Completed cancel → 409" 409 "$(cancelb "$BK")"

echo "--- 4) unconfirm → delivered-1/planned+1/remaining+1 ---"
expect "unconfirm → 200" 200 "$(unconfirm "$SES")"
expectn "delivered=0" 0 "$(lc "$SA" "$LA" deliveredLessonCount)"
expectn "planned=1" 1 "$(lc "$SA" "$LA" plannedLessonCount)"
expectn "remainingLessonRight=10 (+1)" 10 "$(lc "$SA" "$LA" remainingLessonRight)"

echo "--- 5) unconfirm non-Completed (artık Scheduled) → 409 ---"
expect "unconfirm Scheduled → 409" 409 "$(unconfirm "$SES")"

echo "--- 6) unconfirm sonrası Scheduled booking cancel → 200 (planned-1, remaining sabit) ---"
expect "scheduled cancel → 200" 200 "$(cancelb "$BK")"
expectn "planned=0 (cancel)" 0 "$(lc "$SA" "$LA" plannedLessonCount)"
expectn "remainingLessonRight=10 (cancel delivered'ı etkilemez)" 10 "$(lc "$SA" "$LA" remainingLessonRight)"
expectn "availableLessonCount=10 (geri geldi)" 10 "$(lc "$SA" "$LA" availableLessonCount)"

echo "--- 7) Over-planning guard (AvailableLessonCount) ---"
SB=$(guest SB); LB=$(addline "$SB" 1)
SESb=$(mksess "$(date -d "+3 days" +%Y-%m-%dT12:00:00)"); expect "1. booking → 200" 200 "$(bookcode "$SESb" "$SB")"
SESb2=$(mksess "$(date -d "+4 days" +%Y-%m-%dT12:00:00)")
expect "LessonCount aşan 2. booking → 409 (uygun line yok)" 409 "$(bookcode "$SESb2" "$SB")"
expectn "LB availableLessonCount=0" 0 "$(lc "$SB" "$LB" availableLessonCount)"

echo "--- 8) Absent Completed seansta delivered sayılır ---"
SC=$(guest SC); LC2=$(addline "$SC" 5)
SESc=$(mksess "$(date -d "+3 days" +%Y-%m-%dT14:00:00)"); BKc=$(bookid "$SESc" "$SC")
backdate "$SESc"
expect "Absent yoklama → 200" 200 "$(putcode "/api/studio/bookings/$BKc/attendance" '{"status":"Absent"}' "$ATOK")"
expect "confirm → 200" 200 "$(confirm "$SESc")"
expectn "SC delivered=1 (Absent de sayılır)" 1 "$(lc "$SC" "$LC2" deliveredLessonCount)"
expectn "SC remainingLessonRight=4" 4 "$(lc "$SC" "$LC2" remainingLessonRight)"

echo "--- 9) Makeup booking regular planned/delivered/remaining'i değiştirmez ---"
SD=$(guest SD); LD=$(addline "$SD" 5)
pcode "/api/studio/students/$SD/makeup-credits/grant" "{\"studentContractLineId\":\"$LD\",\"amount\":1}" "$ATOK" >/dev/null
SESm=$(mksess "$(date -d "+3 days" +%Y-%m-%dT16:00:00)" Makeup)
expect "makeup booking → 200" 200 "$(bookcode "$SESm" "$SD")"
expectn "LD planned=0 (makeup regular değil)" 0 "$(lc "$SD" "$LD" plannedLessonCount)"
expectn "LD delivered=0" 0 "$(lc "$SD" "$LD" deliveredLessonCount)"
expectn "LD remainingLessonRight=5" 5 "$(lc "$SD" "$LD" remainingLessonRight)"

echo "--- 10) Legacy tek-line null-line booking Planned'a sayılır ---"
SE=$(guest SE); LE=$(addline "$SE" 5); HE=$(hdrof "$SE" "$LE")
SESl=$(mksess "$(date -d "+5 days" +%Y-%m-%dT10:00:00)")
SIDE=$(sql "SELECT \"StudioId\" FROM \"StudentProfiles\" WHERE \"Id\"='$SE';")
sql "INSERT INTO \"ClassBookings\" (\"Id\",\"StudioId\",\"ClassSessionId\",\"StudentProfileId\",\"StudentContractId\",\"Status\",\"BookedByUserId\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$SIDE','$SESl','$SE','$HE','Booked','00000000-0000-0000-0000-000000000000',now(),false);" >/dev/null
expectn "LE planned=1 (legacy null-line tek-line fallback)" 1 "$(lc "$SE" "$LE" plannedLessonCount)"
expectn "LE availableLessonCount=4" 4 "$(lc "$SE" "$LE" availableLessonCount)"

echo "--- 11) Çok-line header null-line booking hiçbir line'a sayılmaz ---"
SF=$(guest SF); LF1=$(addline "$SF" 5); LF2=$(addline "$SF" 5); HF=$(hdrof "$SF" "$LF1")
SESf=$(mksess "$(date -d "+5 days" +%Y-%m-%dT12:00:00)")
SIDF=$(sql "SELECT \"StudioId\" FROM \"StudentProfiles\" WHERE \"Id\"='$SF';")
sql "INSERT INTO \"ClassBookings\" (\"Id\",\"StudioId\",\"ClassSessionId\",\"StudentProfileId\",\"StudentContractId\",\"Status\",\"BookedByUserId\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$SIDF','$SESf','$SF','$HF','Booked','00000000-0000-0000-0000-000000000000',now(),false);" >/dev/null
expectn "LF1 planned=0 (çok-line null-line sayılmaz)" 0 "$(lc "$SF" "$LF1" plannedLessonCount)"
expectn "LF2 planned=0" 0 "$(lc "$SF" "$LF2" plannedLessonCount)"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 29C SMOKE: TÜM TESTLER GEÇTİ ✅"; exit 0; } || { echo "SPRINT 29C SMOKE: BAŞARISIZ ❌"; exit 1; }
