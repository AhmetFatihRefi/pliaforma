#!/usr/bin/env bash
# Pliaforma — Sprint 29B smoke (Candidate Selection: Regular preferred day/time + Makeup separate list):
#  - Regular aday: classType+instructor+tarih+available+preferred-day filtreleri; tercih saatine yakınlık sıralaması;
#    saatsiz slot timed'lardan sonra; slotsuz legacy en sonda; level/çakışma/pending/duplicate/individual elenir.
#  - Makeup aday: gün/saat bağımsız; classType server-side; available>0; çakışma elenir.
#  - Yanlış session kind → 400. Owner/StudioAdmin erişir, Instructor 403. Aynı öğrenci çok line → çok row.
# Postgres + node gerekli. Kullanım: scripts/sprint29b-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
yesno(){ [ "$1" = true ] && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 29B: Candidate Selection (Regular preferred day/time + Makeup) =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S29B $TS\",\"ownerFullName\":\"O\",\"email\":\"o29b+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
inv(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local M; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local T; T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
I1P=$(inv i1); I2P=$(inv i2); I3P=$(inv i3); I1TOK=$(login "i1+$TS@pf.local")
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id; }
CT=$(ctype Mat); CT2=$(ctype Reformer)
TODAY=$(date +%F); END=$(date -d "+45 days" +%F)
THU=$(date -d "next Thursday" +%F); FRI=$(date -d "next Friday" +%F)
REG_AT="${THU}T18:00:00"; S10_AT="${THU}T10:00:00"; SI2_AT="${THU}T18:00:00"; SMK_AT="${FRI}T18:00:00"; SI2F_AT="${FRI}T18:00:00"

guestl(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"${2:+,\"level\":\"$2\"}}" "$ATOK")" | jget id; }
# addline student instr ct slotsJSON [individual] [lessonCount] → line id
addline(){ local cap; [ "${5:-false}" = "true" ] && cap=1 || cap=null; body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$3\",\"instructorProfileId\":\"$2\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":${6:-4},\"pricePerLesson\":100,\"makeupAllowance\":2,\"preferredMaxCapacity\":$cap,\"currency\":\"TRY\",\"preferredSlots\":$4}" "$ATOK")" | jget id; }
legacyline(){ post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$3\",\"instructorProfileId\":\"$2\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"totalLessonCount\":4,\"makeupAllowance\":2,\"price\":400,\"isIndividualDefault\":false}" "$ATOK" >/dev/null; gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data[0]?.id"; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"$3\"${4:+,\"sessionKind\":\"$4\"}}" "$ATOK")" | jget id; }
sbook(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK"; }
grant(){ pcode "/api/studio/students/$1/makeup-credits/grant" "{\"studentContractLineId\":\"$2\",\"amount\":1}" "$ATOK" >/dev/null; }
THU_18_30='[{"dayOfWeek":"Thursday","preferredStartTime":"18:30:00"}]'
THU_20='[{"dayOfWeek":"Thursday","preferredStartTime":"20:00:00"}]'
THU_NULL='[{"dayOfWeek":"Thursday","preferredStartTime":null}]'
THU_18='[{"dayOfWeek":"Thursday","preferredStartTime":"18:00:00"}]'
MON_18='[{"dayOfWeek":"Monday","preferredStartTime":"18:00:00"}]'

# Sessions
SREG=$(mksess "$I1P" "$CT" "$REG_AT"); S10=$(mksess "$I1P" "$CT" "$S10_AT"); SI2=$(mksess "$I2P" "$CT" "$SI2_AT")
SMK=$(mksess "$I1P" "$CT" "$SMK_AT" Makeup); SI2F=$(mksess "$I2P" "$CT" "$SI2F_AT")

# ---- Regular adaylar ----
A=$(guestl A29B Beginner); addline "$A" "$I1P" "$CT" "$THU_18_30" >/dev/null
B=$(guestl B29B); addline "$B" "$I1P" "$CT" "$THU_20" >/dev/null
C=$(guestl C29B); addline "$C" "$I1P" "$CT" "$THU_NULL" >/dev/null
D=$(guestl D29B); legacyline "$D" "$I1P" "$CT" >/dev/null
E=$(guestl E29B); addline "$E" "$I1P" "$CT" "$MON_18" >/dev/null     # gün eşleşmiyor → ele
F=$(guestl F29B); addline "$F" "$I1P" "$CT" "$THU_18" false 1 >/dev/null; sbook "$S10" "$F" >/dev/null   # available 0 → ele
G=$(guestl G29B); addline "$G" "$I1P" "$CT" "$THU_18" true >/dev/null    # individual → grup dersinde ele
H=$(guestl H29B); addline "$H" "$I1P" "$CT" "$THU_18" >/dev/null; addline "$H" "$I2P" "$CT" "$THU_18" >/dev/null; sbook "$SI2" "$H" >/dev/null  # same-time conflict
J=$(guestl J29B); addline "$J" "$I1P" "$CT" "$THU_18" >/dev/null; sbook "$SREG" "$J" >/dev/null   # already booked → ele
# K: I1P/CT aday line + serbest I3P/CT line üzerinden Thu 18:00'a pending makeup talebi → pending conflict ile ele.
K=$(guestl K29B); addline "$K" "$I1P" "$CT" "$THU_18" >/dev/null; KL3=$(addline "$K" "$I3P" "$CT" "$MON_18"); grant "$K" "$KL3"; KREQ=$(pcode /api/studio/makeup-requests "{\"studentContractLineId\":\"$KL3\",\"startAt\":\"$REG_AT\"}" "$ATOK")
Z=$(guestl Z29B); addline "$Z" "$I1P" "$CT" "$THU_18" >/dev/null; addline "$Z" "$I1P" "$CT" "$THU_18" >/dev/null  # 2 line → 2 row

echo "--- 1) Regular aday filtreleri (elenenler) ---"
expect "K pending makeup talebi oluştu (sanity)" 200 "$KREQ"
CSV=$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "d.data.map(c=>c.studentName).join('|')")
idx(){ echo "$CSV" | tr '|' '\n' | grep -nx "$1" | head -1 | cut -d: -f1; }
has(){ echo "$CSV" | tr '|' '\n' | grep -qx "$1" && echo yes || echo no; }
expect "A (gün eşleşen) aday gelir" yes "$(has A29B)"
expect "E (slotlu ama gün eşleşmeyen) gelmez" no "$(has E29B)"
expect "F (AvailableForPlanning=0) gelmez" no "$(has F29B)"
expect "G (bireysel line, grup dersi) gelmez" no "$(has G29B)"
expect "H (same-time conflict) gelmez" no "$(has H29B)"
expect "J (bu seansta zaten booked) gelmez" no "$(has J29B)"
expect "K (pending makeup conflict) gelmez" no "$(has K29B)"
expect "D (slotsuz legacy) aday gelir" yes "$(has D29B)"

echo "--- 2) Regular sıralama ---"
expectn "18:30 tercih (A), 20:00 tercihten (B) üstte" 1 "$([ "$(idx A29B)" -lt "$(idx B29B)" ] && echo 1 || echo 0)"
expectn "timed (B), saatsiz slottan (C) üstte" 1 "$([ "$(idx B29B)" -lt "$(idx C29B)" ] && echo 1 || echo 0)"
expectn "saatsiz slot (C), slotsuz legacy'den (D) üstte" 1 "$([ "$(idx C29B)" -lt "$(idx D29B)" ] && echo 1 || echo 0)"

echo "--- 3) Aynı öğrenci 2 uygun line → 2 ayrı row (aynı studentProfileId) ---"
expectn "Z29B 2 row" 2 "$(echo "$CSV" | tr '|' '\n' | grep -cx Z29B)"
expectn "Z 2 row aynı studentProfileId" 1 "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "(()=>{const z=d.data.filter(c=>c.studentName==='Z29B');return z.length===2 && z[0].studentProfileId===z[1].studentProfileId?1:0})()")"
expect "Z row'ları farklı lineId" true "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "(()=>{const z=d.data.filter(c=>c.studentName==='Z29B');return z[0].lineId!==z[1].lineId})()")"

echo "--- 4) Aday alanları (rozet verisi) ---"
expectn "A matchedDay=true" 1 "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "d.data.find(c=>c.studentName==='A29B')?.matchedDay?1:0")"
expectn "A minutesFromSession=30" 30 "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "d.data.find(c=>c.studentName==='A29B')?.minutesFromSession")"
expect "A nearestPreferredTime 18:30" true "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "d.data.find(c=>c.studentName==='A29B')?.nearestPreferredTime?.slice(0,5)==='18:30'")"
expectn "D hasPreferredSlots=false" 1 "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "d.data.find(c=>c.studentName==='D29B')?.hasPreferredSlots===false?1:0")"
expectn "C nearestPreferredTime null (saatsiz)" 1 "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK" | jq2 "d.data.find(c=>c.studentName==='C29B')?.nearestPreferredTime==null?1:0")"

echo "--- 5) Level filtresi ---"
CSVB=$(gbody "/api/studio/class-sessions/$SREG/regular-candidates?level=Beginner" "$ATOK" | jq2 "d.data.map(c=>c.studentName).join('|')")
expect "level=Beginner → A var" yes "$(echo "$CSVB" | tr '|' '\n' | grep -qx A29B && echo yes || echo no)"
expect "level=Beginner → B (levelsiz) yok" no "$(echo "$CSVB" | tr '|' '\n' | grep -qx B29B && echo yes || echo no)"
expect "level=Intermediate → A yok" false "$(gbody "/api/studio/class-sessions/$SREG/regular-candidates?level=Intermediate" "$ATOK" | jq2 "d.data.some(c=>c.studentName==='A29B')")"

echo "--- 6) Makeup adaylar (gün/saat bağımsız, classType server-side, conflict) ---"
MA=$(guestl MA29B); MAL=$(addline "$MA" "$I1P" "$CT" "$MON_18"); grant "$MA" "$MAL"   # Monday slot ama makeup gün-bağımsız
MB=$(guestl MB29B); MBL=$(addline "$MB" "$I1P" "$CT2" "$MON_18"); grant "$MB" "$MBL"   # classType CT2 ≠ CT → ele
MC=$(guestl MC29B); addline "$MC" "$I1P" "$CT" "$MON_18" >/dev/null                    # kredi YOK → available 0 → ele
MD=$(guestl MD29B); MDL=$(addline "$MD" "$I1P" "$CT" "$MON_18"); grant "$MD" "$MDL"; addline "$MD" "$I2P" "$CT" "$MON_18" >/dev/null; sbook "$SI2F" "$MD" >/dev/null  # FRI 18:00 conflict → ele
MCSV=$(gbody "/api/studio/class-sessions/$SMK/makeup-candidates" "$ATOK" | jq2 "d.data.map(c=>c.studentName).join('|')")
mhas(){ echo "$MCSV" | tr '|' '\n' | grep -qx "$1" && echo yes || echo no; }
expect "MA (CT + kredi) makeup aday gelir (gün-bağımsız)" yes "$(mhas MA29B)"
expect "MB (CT2 classType mismatch) gelmez" no "$(mhas MB29B)"
expect "MC (kredi yok) gelmez" no "$(mhas MC29B)"
expect "MD (same-time conflict) gelmez" no "$(mhas MD29B)"

echo "--- 7) Yanlış session kind → 400 ---"
expect "regular-candidates Makeup seansta → 400" 400 "$(gcode "/api/studio/class-sessions/$SMK/regular-candidates" "$ATOK")"
expect "makeup-candidates Regular seansta → 400" 400 "$(gcode "/api/studio/class-sessions/$SREG/makeup-candidates" "$ATOK")"

echo "--- 8) Auth: Owner erişir, Instructor 403 ---"
expect "Owner regular-candidates → 200" 200 "$(gcode "/api/studio/class-sessions/$SREG/regular-candidates" "$ATOK")"
expect "Owner makeup-candidates → 200" 200 "$(gcode "/api/studio/class-sessions/$SMK/makeup-candidates" "$ATOK")"
expect "Instructor regular-candidates → 403" 403 "$(gcode "/api/studio/class-sessions/$SREG/regular-candidates" "$I1TOK")"
expect "Instructor makeup-candidates → 403" 403 "$(gcode "/api/studio/class-sessions/$SMK/makeup-candidates" "$I1TOK")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 29B SMOKE: TÜM TESTLER GEÇTİ ✅"; exit 0; } || { echo "SPRINT 29B SMOKE: BAŞARISIZ ❌"; exit 1; }
