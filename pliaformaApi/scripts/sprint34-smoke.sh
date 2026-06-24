#!/usr/bin/env bash
# Pliaforma — Sprint 34 smoke (Sözleşme satırı tercih güncelleme + kalan dersleri yeniden planla):
#  A. Preference update: hoca null/dolu/yabancı(404); kapasite 0/51(400)/2(200); slot full-replace; booking'e dokunmaz.
#  C. Replan future-only: Completed seans + booking DEĞİŞMEZ; gelecekteki Scheduled regular booking'ler iptal + yeniden üret; Delivered sabit.
#  D. Shared group safety: paylaşılan seans taşınmaz; diğer öğrencinin booking'i etkilenmez; yalnız bu öğrenci ayrılır.
#  E. Equipment guard: ekipman-bağlı + tercih null + replan capacity'siz → 409; capacity ile → 200, Capacity=seçilen (9999 değil).
#  F. Instructor guard: line hoca null + replan hoca seçili → o hocaya planlar.
# ASCII-only JSON. Postgres + node gerekli. Migration YOK (Sprint 34).
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putb(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
TS=$(date +%s); TODAY=$(date +%F); END=$(date -d "+120 days" +%F)
ipid(){ gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 34: tercih güncelle + replan =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S34 $TS\",\"ownerFullName\":\"O\",\"email\":\"o34+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
mkinst(){ e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
IA=$(mkinst hocaA); IB=$(mkinst hocaB)
EQ=$(body "$(post /api/studio/equipment "{\"name\":\"Reformer\",\"quantity\":4,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
CTREF=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQ\"}" "$ATOK")" | jget id)
CTMAT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
# addline: $1=student $2=classType $3=instr("" null) $4=cap(null) $5=slotGün(Mon) $6=slotSaat(10:00) $7=lessonCount(8)
addline(){ local instr="${3:+\"$3\"}"; instr="${instr:-null}"; local cap="${4:-null}"; post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":$instr,\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":${7:-8},\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":$cap,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"${5:-Monday}\",\"preferredStartTime\":\"${6:-10:00}:00\"}]}" "$ATOK"; }
genline(){ pcode "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"${2:-Monday}\",\"startTime\":\"${3:-10:00}:00\"}],\"reuseExistingGroup\":true${4:+,\"instructorProfileId\":\"$4\"}${5:+,\"capacity\":$5}}" "$ATOK"; }
prefs(){ putc "/api/studio/contract-lines/$1/preferences" "$2" "$ATOK"; }   # $2=body
replan_body(){ local p=""; [ -n "${1:-}" ] && p="\"instructorProfileId\":\"$1\""; [ -n "${2:-}" ] && p="${p:+$p,}\"capacity\":$2"; echo "{$p}"; }
replan(){ pcode "/api/studio/contract-lines/$1/replan" "$(replan_body "${2:-}" "${3:-}")" "$ATOK"; }
replanB(){ post "/api/studio/contract-lines/$1/replan" "$(replan_body "${2:-}" "${3:-}")" "$ATOK"; }
cntBookedFuture(){ sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractLineId\"='$1' AND b.\"Status\"='Booked' AND s.\"SessionKind\"='Regular' AND s.\"Status\"='Scheduled';"; }
cntDelivered(){ sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractLineId\"='$1' AND b.\"Status\"='Booked' AND s.\"Status\"='Completed';"; }

echo "--- A) Preference update ---"
SA=$(guest A34); LA=$(body "$(addline "$SA" "$CTMAT" "$IA" 2)" | jget id)
expect "A: hoca null'a güncelle → 200" 200 "$(prefs "$LA" "{\"instructorProfileId\":null,\"preferredMaxCapacity\":2,\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null}]}")"
expect "A: line.instructorProfileId artık null" "" "$(gbody "/api/studio/students/$SA/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LA').instructorProfileId")"
expect "A: hoca dolu'ya güncelle → 200" 200 "$(prefs "$LA" "{\"instructorProfileId\":\"$IB\",\"preferredMaxCapacity\":2,\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null}]}")"
expect "A: line.instructorProfileId = IB" "$IB" "$(gbody "/api/studio/students/$SA/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LA').instructorProfileId")"
expect "A: yabancı hoca → 404" 404 "$(prefs "$LA" "{\"instructorProfileId\":\"00000000-0000-0000-0000-000000000999\",\"preferredMaxCapacity\":2,\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null}]}")"
expect "A: kapasite 0 → 400" 400 "$(prefs "$LA" "{\"instructorProfileId\":null,\"preferredMaxCapacity\":0,\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null}]}")"
expect "A: kapasite 51 → 400" 400 "$(prefs "$LA" "{\"instructorProfileId\":null,\"preferredMaxCapacity\":51,\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null}]}")"
expect "A: slot yok → 400" 400 "$(prefs "$LA" "{\"instructorProfileId\":null,\"preferredMaxCapacity\":2,\"preferredSlots\":[]}")"
expect "A: aynı gün/saat dup → 400" 400 "$(prefs "$LA" "{\"instructorProfileId\":null,\"preferredMaxCapacity\":2,\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null},{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"10:00:00\",\"notes\":null}]}")"
# slot full-replace: Wed 18:00 → tek slot
prefs "$LA" "{\"instructorProfileId\":null,\"preferredMaxCapacity\":2,\"preferredSlots\":[{\"dayOfWeek\":\"Wednesday\",\"preferredStartTime\":\"18:00:00\",\"notes\":null}]}" >/dev/null
expectn "A: slot full-replace → tek slot (Wed)" 1 "$(gbody "/api/studio/students/$SA/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LA').preferredSlots.length")"
expect "A: kalan slot Wednesday" Wednesday "$(gbody "/api/studio/students/$SA/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LA').preferredSlots[0].dayOfWeek")"
# preference update booking'i bozmaz: generate sonrası prefs update → Planned sabit
SP=$(guest P34); LP=$(body "$(addline "$SP" "$CTMAT" "$IA" "")" | jget id); genline "$LP" Monday 10:00 >/dev/null
BEFORE=$(cntBookedFuture "$LP")
prefs "$LP" "{\"instructorProfileId\":\"$IB\",\"preferredMaxCapacity\":3,\"preferredSlots\":[{\"dayOfWeek\":\"Friday\",\"preferredStartTime\":\"11:00:00\",\"notes\":null}]}" >/dev/null
expectn "A: prefs update Planned booking'i DEĞİŞTİRMEZ" "$BEFORE" "$(cntBookedFuture "$LP")"

echo "--- C) Replan future-only (Completed/Delivered korunur) ---"
SC=$(guest C34); LC=$(body "$(addline "$SC" "$CTMAT" "$IA" "" Monday 10:00 4)" | jget id)
genline "$LC" Monday 10:00 >/dev/null
GEN=$(cntBookedFuture "$LC"); echo "    (üretilen future booking: $GEN)"
# en erken seansı Completed yap (delivered simülasyonu) — booking Booked kalır
sql "UPDATE \"ClassSessions\" SET \"Status\"='Completed' WHERE \"Id\"=(SELECT s.\"Id\" FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LC' AND b.\"Status\"='Booked' ORDER BY s.\"StartAt\" LIMIT 1);" >/dev/null
DEL_BEFORE=$(cntDelivered "$LC")
expectn "C: 1 ders Completed (delivered)" 1 "$DEL_BEFORE"
expect "C: replan (aynı hoca/kapasite) → 200" 200 "$(replan "$LC")"
expectn "C: Delivered DEĞİŞMEDİ (Completed korunur)" 1 "$(cntDelivered "$LC")"
expectn "C: kalan future planlandı (LessonCount - delivered = 3)" 3 "$(cntBookedFuture "$LC")"

echo "--- D) Shared group safety ---"
SD1=$(guest D1_34); LD1=$(body "$(addline "$SD1" "$CTMAT" "$IA" "" Monday 14:00)" | jget id)
SD2=$(guest D2_34); LD2=$(body "$(addline "$SD2" "$CTMAT" "$IA" "" Monday 14:00)" | jget id)
genline "$LD1" Monday 14:00 >/dev/null
genline "$LD2" Monday 14:00 >/dev/null   # reuse → aynı grup seansına bağlanır
# ortak seans id (LD1'in ilk seansı)
SHARED=$(sql "SELECT s.\"Id\" FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LD1' AND b.\"Status\"='Booked' ORDER BY s.\"StartAt\" LIMIT 1;")
SHARED_BC=$(sql "SELECT \"BookedCount\" FROM \"ClassSessions\" WHERE \"Id\"='$SHARED';")
echo "    (ortak seans $SHARED BookedCount=$SHARED_BC)"
expectn "D: ortak seans 2 öğrenci" 2 "$SHARED_BC"
# SD1'i Çarşamba'ya taşı (prefs) sonra replan
prefs "$LD1" "{\"instructorProfileId\":\"$IA\",\"preferredMaxCapacity\":null,\"preferredSlots\":[{\"dayOfWeek\":\"Wednesday\",\"preferredStartTime\":\"14:00:00\",\"notes\":null}]}" >/dev/null
replan "$LD1" >/dev/null
expect "D: ortak seans HALA Scheduled (taşınmadı/iptal olmadı)" Scheduled "$(sql "SELECT \"Status\" FROM \"ClassSessions\" WHERE \"Id\"='$SHARED';")"
expect "D: SD2 booking'i HALA Booked (etkilenmedi)" Booked "$(sql "SELECT \"Status\" FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$SHARED' AND \"StudentContractLineId\"='$LD2';")"
expect "D: SD1 booking'i ortak seanstan AYRILDI (CancelledByStudio)" CancelledByStudio "$(sql "SELECT \"Status\" FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$SHARED' AND \"StudentContractLineId\"='$LD1';")"
expectn "D: ortak seans BookedCount=1 (yalnız SD2)" 1 "$(sql "SELECT \"BookedCount\" FROM \"ClassSessions\" WHERE \"Id\"='$SHARED';")"
expect "D: SD1 yeni Çarşamba seansı oluştu" Wednesday "$(sql "SELECT to_char(s.\"StartAt\",'Day') FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LD1' AND b.\"Status\"='Booked' AND s.\"Status\"='Scheduled' ORDER BY s.\"StartAt\" LIMIT 1;" | tr -d ' ')"

echo "--- E) Equipment guard ---"
SE=$(guest E34); LE=$(body "$(addline "$SE" "$CTREF" "$IA" "" Monday 16:00)" | jget id)   # ekipman-bağlı + tercih null
expect "E: ekipman-bağlı + tercih null, capacity'siz replan → 409" 409 "$(replan "$LE")"
expect "E: capacity=2 ile replan → 200" 200 "$(replan "$LE" "" 2)"
expectn "E: üretilen ekipman seansı Capacity=2 (9999 değil)" 2 "$(sql "SELECT s.\"Capacity\" FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LE' AND b.\"Status\"='Booked' AND s.\"Status\"='Scheduled' ORDER BY s.\"StartAt\" LIMIT 1;")"
expectn "E: 32B invariant — ekipman-bağlı 9999 seans YOK" 0 "$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"ClassTypeId\"='$CTREF' AND \"Capacity\">=9999;")"

echo "--- F) Instructor guard (line hoca null) ---"
SF=$(guest F34); LF=$(body "$(addline "$SF" "$CTMAT" "" "" Monday 17:00)" | jget id)   # hoca null
expect "F: hoca null line, hocasız replan → 409" 409 "$(replan "$LF")"
expect "F: hoca IB ile replan → 200" 200 "$(replan "$LF" "$IB")"
expectn "F: üretilen seanslar IB hocasına ait" 1 "$(sql "SELECT CASE WHEN count(*)>0 AND bool_and(s.\"InstructorProfileId\"='$IB') THEN 1 ELSE 0 END FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LF' AND b.\"Status\"='Booked' AND s.\"Status\"='Scheduled';")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 34 SMOKE: TUM TESTLER GECTI ✅"; exit 0; } || { echo "SPRINT 34 SMOKE: BASARISIZ ❌"; exit 1; }
