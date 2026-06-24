#!/usr/bin/env bash
# Pliaforma — Sprint 33 smoke (Sözleşmede opsiyonel hoca + kapasite tercihi):
#  A. Create validation: hoca opsiyonel; PreferredMaxCapacity 1..50 (0/51 → 400).
#  B. Candidate/booking (HARD): line hoca null → farklı hocalı dersler aday; line hoca dolu → sadece aynı hoca.
#     PreferredMaxCapacity=2 → cap1/2 booking OK, cap3 booking 409; null → kapasite filtresi yok.
#  C. Program generate (effective hoca/kapasite):
#     hoca null → instructor'sız generate 409; instructor ile 200.
#     PreferredMaxCapacity=1 → seans Capacity=1; =2 → Capacity=2; null+ekipmansız → 9999.
#     null+ekipman-bağlı → capacity'siz generate 409; capacity ile 200 (32B guard).
# ASCII-only JSON. Postgres + node gerekli.
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null 2>&1; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
TODAY=$(date +%F); END=$(date -d "+90 days" +%F)
D2=$(date -d "+2 days" +%F); D2DOW=$(date -d "+2 days" +%A)   # session günü = D2'nin İngilizce haftagünü (candidate gün-filtresi için)

echo "== Sprint 33: opsiyonel hoca + kapasite tercihi =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S33 $TS\",\"ownerFullName\":\"O\",\"email\":\"o33+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
mkinst(){ e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
IA=$(mkinst hocaA); IB=$(mkinst hocaB)
EQ=$(body "$(post /api/studio/equipment "{\"name\":\"Reformer\",\"quantity\":4,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
CTREF=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQ\"}" "$ATOK")" | jget id)
CTMAT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
# addline: $1=student $2=classType $3=instructor("" = null) $4=preferredMaxCapacity("" = null) $5=slotGünü(default Monday)
addline(){ local instr="${3:+\"$3\"}"; instr="${instr:-null}"; local cap="${4:-null}"; local day="${5:-Monday}"; post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":$instr,\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":$cap,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"$day\",\"preferredStartTime\":null}]}" "$ATOK"; }
mksess(){ post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"${D2}T$3:00\",\"capacity\":$4}" "$ATOK"; }
sbook(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\",\"studentContractLineId\":\"$3\"}" "$ATOK"; }
genline(){ pcode "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"$2:00\"}],\"reuseExistingGroup\":false${3:+,\"instructorProfileId\":\"$3\"}${4:+,\"capacity\":$4}}" "$ATOK"; }
sesscap(){ gbody "/api/studio/class-sessions?from=${TODAY}T00:00:00&to=$(date -d '+30 days' +%F)T00:00:00&instructorProfileId=$2" "$ATOK" | jq2 "d.data.find(s=>s.classTypeId==='$1')?.capacity"; }

echo "--- A) Create validation ---"
SA=$(guest A33)
expect "hocasiz + tercihsiz line → 200" 200 "$(code "$(addline "$SA" "$CTMAT" "" "")")"
expect "PreferredMaxCapacity=2 line → 200" 200 "$(code "$(addline "$SA" "$CTMAT" "$IA" 2)")"
expect "PreferredMaxCapacity=0 → 400" 400 "$(pcode "/api/studio/students/$SA/contract-lines" "{\"classTypeId\":\"$CTMAT\",\"instructorProfileId\":null,\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":0,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]}" "$ATOK")"
expect "PreferredMaxCapacity=51 → 400" 400 "$(pcode "/api/studio/students/$SA/contract-lines" "{\"classTypeId\":\"$CTMAT\",\"instructorProfileId\":null,\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":51,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]}" "$ATOK")"

echo "--- B) Candidate (hoca) + booking (kapasite HARD) ---"
# line hoca null → herhangi bir hocanın Mat dersine aday
SB=$(guest B33); LBn=$(body "$(addline "$SB" "$CTMAT" "" "" "$D2DOW")" | jget id)
SMAT_IA=$(body "$(mksess "$IA" "$CTMAT" "10:00" 9999)" | jget id)
SMAT_IB=$(body "$(mksess "$IB" "$CTMAT" "12:00" 9999)" | jget id)
expect "B: hocasiz line IA dersinde aday" yes "$(gbody "/api/studio/class-sessions/$SMAT_IA/regular-candidates" "$ATOK" | jq2 "d.data.some(c=>c.studentProfileId==='$SB')?'yes':'no'")"
expect "B: hocasiz line IB dersinde de aday" yes "$(gbody "/api/studio/class-sessions/$SMAT_IB/regular-candidates" "$ATOK" | jq2 "d.data.some(c=>c.studentProfileId==='$SB')?'yes':'no'")"
# line hoca = IA → yalnız IA dersinde aday
SC=$(guest C33); LCa=$(body "$(addline "$SC" "$CTMAT" "$IA" "" "$D2DOW")" | jget id)
expect "B: IA-pinli line IA dersinde aday" yes "$(gbody "/api/studio/class-sessions/$SMAT_IA/regular-candidates" "$ATOK" | jq2 "d.data.some(c=>c.studentProfileId==='$SC')?'yes':'no'")"
expect "B: IA-pinli line IB dersinde aday DEGIL" no "$(gbody "/api/studio/class-sessions/$SMAT_IB/regular-candidates" "$ATOK" | jq2 "d.data.some(c=>c.studentProfileId==='$SC')?'yes':'no'")"
# kapasite HARD: PreferredMaxCapacity=2 → cap2 booking OK, cap3 booking 409
SD=$(guest D33); LD2=$(body "$(addline "$SD" "$CTMAT" "$IA" 2)" | jget id)
S_CAP2=$(body "$(mksess "$IA" "$CTMAT" "14:00" 2)" | jget id)
S_CAP3=$(body "$(mksess "$IB" "$CTMAT" "15:00" 3)" | jget id)
expect "B: tercih2 + cap2 derse ekle → 200" 200 "$(sbook "$S_CAP2" "$SD" "$LD2")"
expect "B: tercih2 + cap3 derse ekle → 409" 409 "$(sbook "$S_CAP3" "$SD" "$LD2")"

echo "--- C) Program generate (effective hoca/kapasite) ---"
# hoca null Mat line → instructor'sız generate 409; instructor ile 200
SE=$(guest E33); LEn=$(body "$(addline "$SE" "$CTMAT" "" "")" | jget id)
expect "C: hocasiz line, instructor'siz generate → 409" 409 "$(genline "$LEn" "08:00")"
expect "C: hocasiz line, instructor ile generate → 200" 200 "$(genline "$LEn" "08:00" "$IA")"
# PreferredMaxCapacity=1 → Capacity=1 (taze hoca IF1 — IA/IB'de eski 9999 Mat seansı var, sesscap.find() yanlış seansı bulmasın)
IF1=$(mkinst hocaF); SF=$(guest F33); LF1=$(body "$(addline "$SF" "$CTMAT" "$IF1" 1)" | jget id)
genline "$LF1" "09:00" >/dev/null
expectn "C: tercih=1 → uretilen Mat seans Capacity=1 (IF1)" 1 "$(sesscap "$CTMAT" "$IF1")"
# PreferredMaxCapacity=2 → Capacity=2 (ayri hoca IC)
IC=$(mkinst hocaC); SG=$(guest G33); LG2=$(body "$(addline "$SG" "$CTMAT" "$IC" 2)" | jget id)
genline "$LG2" "09:00" >/dev/null
expectn "C: tercih=2 → uretilen Mat seans Capacity=2 (IC)" 2 "$(sesscap "$CTMAT" "$IC")"
# null + ekipmansiz → 9999 (ayri hoca ID)
ID=$(mkinst hocaD); SH=$(guest H33); LHn=$(body "$(addline "$SH" "$CTMAT" "$ID" "")" | jget id)
genline "$LHn" "09:00" >/dev/null
expectn "C: tercih yok + ekipmansiz → Capacity=9999 (ID)" 9999 "$(sesscap "$CTMAT" "$ID")"
# Reformer (ekipman-bagli) + tercih yok → capacity'siz generate 409; capacity ile 200
IE=$(mkinst hocaE); SI=$(guest I33); LIr=$(body "$(addline "$SI" "$CTREF" "$IE" "")" | jget id)
expect "C: ekipman-bagli + tercih yok, capacity'siz generate → 409" 409 "$(genline "$LIr" "09:00")"
expect "C: ekipman-bagli + tercih yok, capacity=2 ile generate → 200" 200 "$(genline "$LIr" "09:00" "" 2)"
expectn "C: ekipman generate seans Capacity=2 (IE)" 2 "$(sesscap "$CTREF" "$IE")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 33 SMOKE: TUM TESTLER GECTI ✅"; exit 0; } || { echo "SPRINT 33 SMOKE: BASARISIZ ❌"; exit 1; }
