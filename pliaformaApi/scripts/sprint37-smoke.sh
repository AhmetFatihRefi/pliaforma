#!/usr/bin/env bash
# Pliaforma — Sprint 37 smoke (Stüdyo çalışma saatleri):
#  A. CRUD: kayıt yokken GET → isConfigured=false + fallback 07:00-23:00; PUT 7 gun (Pzt-Cmt 09-22, Paz kapali) → GET configured;
#     acik gunde saat eksik → 400; acilis>=kapanis → 400.
#  B. Availability: 09-22 stüdyo → Pazartesi pencere 09:00'da baslar (07:00 yok); Pazar (kapali) → bos.
#  C. Manuel create/update/reschedule: ic saat → 200; acilis oncesi → 409; bitis kapanisi asar → 409; end==close → 200;
#     kapali gun → 409; reschedule pencere disi → 409.
#  D. Generate/replan: ic slot üretir; pencere disi slot skippedOutsideWorkingHours; tüm slotlar disi → created=0.
#  P. Yetki: PUT working-hours instructor → 403; GET → 200.
# Fallback: kayit yoksa 07:00-23:00. ASCII-only. Postgres+node yok (curl+grep). Onceki sprint desenleri.
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$2" | grep -q "$3" && ok "$1" || bad "$1 (icermiyor: $3)"; }
hasnot(){ echo "$2" | grep -q "$3" && bad "$1 (icermemeli: $3)" || ok "$1"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":[0-9]*" | head -1 | sed "s/\"$1\"://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putb(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
sqlv(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sqlv "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
TS=$(date +%s)
MON=$(date -d "next Monday" +%F); TUE=$(date -d "next Tuesday" +%F); SUN=$(date -d "next Sunday" +%F)
END=$(date -d "+200 days" +%F)
ipid(){ gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 37: stüdyo çalışma saatleri =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S37 $TS\",\"ownerFullName\":\"O\",\"email\":\"o37+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 2; }
mkinst(){ e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
IA=$(mkinst hocaA); IB=$(mkinst hocaB)
ITOK=$(login "hocaA+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }

# WH JSON helpers
WD='{"dayOfWeek":"%s","isClosed":false,"opensAt":"09:00","closesAt":"22:00"}'
wday(){ printf "$WD" "$1"; }
WH7="{\"days\":[$(wday Monday),$(wday Tuesday),$(wday Wednesday),$(wday Thursday),$(wday Friday),$(wday Saturday),{\"dayOfWeek\":\"Sunday\",\"isClosed\":true,\"opensAt\":null,\"closesAt\":null}]}"

echo "--- A) Working hours CRUD ---"
GW0=$(gbody /api/studio/working-hours "$ATOK")
has   "A: kayit yokken GET 200 + isConfigured=false" "$GW0" '"isConfigured":false'
has   "A: fallback 07:00 acilis" "$GW0" '07:00'
has   "A: fallback 23:00 kapanis" "$GW0" '23:00'
expect "A: PUT 7 gun (Pzt-Cmt 09-22, Paz kapali) → 200" 200 "$(putc /api/studio/working-hours "$WH7" "$ATOK")"
GW1=$(gbody /api/studio/working-hours "$ATOK")
has   "A: GET sonrasi isConfigured=true" "$GW1" '"isConfigured":true'
has   "A: Pazar kapali (isClosed true)" "$GW1" '"isClosed":true'
has   "A: 09:00 acilis kayitli" "$GW1" '09:00'
# acik gunde saat eksik → 400
WHBAD1="{\"days\":[{\"dayOfWeek\":\"Monday\",\"isClosed\":false,\"opensAt\":null,\"closesAt\":null},$(wday Tuesday),$(wday Wednesday),$(wday Thursday),$(wday Friday),$(wday Saturday),{\"dayOfWeek\":\"Sunday\",\"isClosed\":true,\"opensAt\":null,\"closesAt\":null}]}"
expect "A: acik gunde saat eksik → 400" 400 "$(putc /api/studio/working-hours "$WHBAD1" "$ATOK")"
# acilis>=kapanis → 400
WHBAD2="{\"days\":[{\"dayOfWeek\":\"Monday\",\"isClosed\":false,\"opensAt\":\"22:00\",\"closesAt\":\"09:00\"},$(wday Tuesday),$(wday Wednesday),$(wday Thursday),$(wday Friday),$(wday Saturday),{\"dayOfWeek\":\"Sunday\",\"isClosed\":true,\"opensAt\":null,\"closesAt\":null}]}"
expect "A: acilis>=kapanis → 400" 400 "$(putc /api/studio/working-hours "$WHBAD2" "$ATOK")"

echo "--- P) Yetki ---"
expect "P: GET working-hours instructor → 200" 200 "$(gcode /api/studio/working-hours "$ITOK")"
expect "P: PUT working-hours instructor → 403" 403 "$(putc /api/studio/working-hours "$WH7" "$ITOK")"

echo "--- B) Availability ---"
AV_MON=$(gbody "/api/studio/availability/class-type-slots?classTypeId=$CT&from=$MON&to=$MON&durationMinutes=60&requiredCapacity=1" "$ATOK")
has    "B: Pazartesi pencere 09:00'da baslar" "$AV_MON" 'T09:00'
hasnot "B: Pazartesi 07:00 boslugu YOK" "$AV_MON" 'T07:00'
AV_SUN=$(gbody "/api/studio/availability/class-type-slots?classTypeId=$CT&from=$SUN&to=$SUN&durationMinutes=60&requiredCapacity=1" "$ATOK")
has    "B: Pazar (kapali) → bos veri dizisi" "$AV_SUN" '"data":\[\]'

echo "--- C) Manuel create/update/reschedule ---"
mk(){ pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$IA\",\"classTypeId\":\"$CT\",\"startAt\":\"$1\"}" "$ATOK"; }
mkid(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$IA\",\"classTypeId\":\"$CT\",\"startAt\":\"$1\"}" "$ATOK")" | jget id; }
expect "C: Pzt 10:00 (ic) → 200" 200 "$(mk "${MON}T10:00:00")"
expect "C: Pzt 08:00 (acilis oncesi) → 409" 409 "$(mk "${MON}T08:00:00")"
expect "C: Pzt 21:30 60dk (bitis 22:30 kapanis asar) → 409" 409 "$(mk "${MON}T21:30:00")"
expect "C: Pzt 22:00 (start>=kapanis) → 409" 409 "$(mk "${MON}T22:00:00")"
expect "C: Pzt 21:00 60dk (end==22:00 kapanis kabul) → 200" 200 "$(mk "${MON}T21:00:00")"
expect "C: Pazar 10:00 (kapali gun) → 409" 409 "$(mk "${SUN}T10:00:00")"
SID=$(mkid "${MON}T12:00:00")
expect "C: 12:00 seans olustu" 1 "$([ -n "$SID" ] && echo 1 || echo 0)"
expect "C: reschedule 08:00 (pencere disi) → 409" 409 "$(pcode "/api/studio/class-sessions/$SID/reschedule" "{\"newStartAt\":\"${MON}T08:00:00\",\"keepBookings\":false}" "$ATOK")"
expect "C: reschedule 14:00 (ic) → 200" 200 "$(pcode "/api/studio/class-sessions/$SID/reschedule" "{\"newStartAt\":\"${MON}T14:00:00\",\"keepBookings\":false}" "$ATOK")"

echo "--- D) Generate / replan ---"
S(){ echo "{\"dayOfWeek\":\"$1\",\"preferredStartTime\":\"$2\",\"notes\":null}"; }
addlineS(){ post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$2\",\"startDate\":\"$MON\",\"endDate\":\"$END\",\"lessonCount\":$3,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":null,\"currency\":\"TRY\",\"preferredSlots\":$4}" "$ATOK"; }
genB(){ local gs=$(echo "$2" | sed 's/preferredStartTime/startTime/g'); body "$(post "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":$gs,\"reuseExistingGroup\":true}" "$ATOK")"; }
# D1: Pzt 10:00 (ic) + Sal 23:00 (kapanis sonrasi) → Pzt'ler üretilir, Sal'lar skippedOutsideWorkingHours
SG=$(guest D37a); MIX="[$(S Monday 10:00:00),$(S Tuesday 23:00:00)]"; LG=$(body "$(addlineS "$SG" "$IB" 4 "$MIX")" | jget id)
RG=$(genB "$LG" "$MIX")
expectn "D1: createdCount=4 (Pzt ic slotlar)" 4 "$(echo "$RG" | jnum createdCount)"
awk "BEGIN{exit !($(echo "$RG" | jnum skippedOutsideWorkingHoursCount)>0)}" 2>/dev/null && ok "D1: skippedOutsideWorkingHours>0 (Sal 23:00)" || bad "D1: skippedOutsideWorkingHours>0 (gelen $(echo "$RG" | jnum skippedOutsideWorkingHoursCount))"
# D2: tüm slotlar kapali gunde (Pazar) → created=0, skippedOutsideWorkingHours>0
SH=$(guest D37b); SUNS="[$(S Sunday 10:00:00)]"; LH=$(body "$(addlineS "$SH" "$IB" 3 "$SUNS")" | jget id)
RH=$(genB "$LH" "$SUNS")
expectn "D2: created=0 (hepsi kapali gun)" 0 "$(echo "$RH" | jnum createdCount)"
awk "BEGIN{exit !($(echo "$RH" | jnum skippedOutsideWorkingHoursCount)>0)}" 2>/dev/null && ok "D2: skippedOutsideWorkingHours>0 (kapali gun)" || bad "D2: skippedOutsideWorkingHours>0"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 37 SMOKE: TUM TESTLER GECTI"; exit 0; } || { echo "SPRINT 37 SMOKE: BASARISIZ"; exit 1; }
