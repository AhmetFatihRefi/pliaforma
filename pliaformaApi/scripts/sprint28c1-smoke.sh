#!/usr/bin/env bash
# Pliaforma — Sprint 28C-1 smoke (Regular Scheduling/Booking LINE-bazlı + Individual Hard Rule + Student Level Filter):
#  - Line generate → booking.StudentContractLineId yazar; available LINE bazlı düşer; lessonCount aşımı/tarih-dışı → 409.
#  - Regular reschedule + chain-shift line.EndDate dışına → 409 (hard block).
#  - Bireysel line grup dersine atanamaz (409); bireysel seansa 2. öğrenci 409; group generation bireysel line'ı gruba koymaz.
#  - Group schedule change lineId ile çalışır. Level filtresi öğrenci listesini süzer.
#  - Legacy null-line booking fallback YALNIZ tek-line header'da sayılır; çok-line header'da hiçbir line'a sayılmaz.
#  - Regresyon: v1.4.0 line payment/debt; v1.2.0 makeup request eski akış; v1.1.2 rol hardening.
# Postgres + node gerekli. Kullanım: scripts/sprint28c1-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"${2:+,\"level\":\"$2\"}}" "$ATOK")" | jget id; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S28C1 $TS\",\"ownerFullName\":\"O\",\"email\":\"o28c1+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
invinstr(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invinstr i1); I1TOK=$(login "i1+$TS@pf.local"); I2P=$(invinstr i2)
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id; }
CT=$(ctype Mat)
TODAY=$(date +%F); SOON=$(date -d "+2 days" +%F); END=$(date -d "+60 days" +%F); FAR=$(date -d "+70 days" +%F)
ALLDAYS='[{"dayOfWeek":"Monday","startTime":"18:00:00"},{"dayOfWeek":"Tuesday","startTime":"18:00:00"},{"dayOfWeek":"Wednesday","startTime":"18:00:00"},{"dayOfWeek":"Thursday","startTime":"18:00:00"},{"dayOfWeek":"Friday","startTime":"18:00:00"},{"dayOfWeek":"Saturday","startTime":"18:00:00"},{"dayOfWeek":"Sunday","startTime":"18:00:00"}]'
# addline: $1=student $2=instructorProfile $3=lessonCount $4=isIndividual
addline(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$2\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$3,\"pricePerLesson\":100,\"makeupAllowance\":1,\"isIndividual\":$4,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]}" "$ATOK")"; }
genline(){ post "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":$ALLDAYS,\"reuseExistingGroup\":false}" "$ATOK"; }
lineavail(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.availableLessonCount"; }
linesched(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.scheduledLessonCount"; }
# mksession: $1=instr $2=date $3=saat (vars. 12:00 — generate 18:00'lerle çakışmasın)
mksession(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"${2}T${3:-12:00}:00\"}" "$ATOK")" | jget id; }
bookcode(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"${3:+,\"studentContractLineId\":\"$3\"}}" "$ATOK"; }

echo "--- 1) Line generate → booking.StudentContractLineId yazar + available LINE bazlı düşer ---"
SA=$(guest SA); LA=$(addline "$SA" "$I1P" 4 false | jget id)
RG=$(genline "$LA"); expect "generate → 200" 200 "$(code "$RG")"
expectn "4 booking üretildi" 4 "$(body "$RG" | jq2 'd.data.createdCount')"
expectn "tüm üretilen booking lineId taşır (NULL yok)" 0 "$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"StudentContractId\"=(SELECT \"StudentContractId\" FROM \"StudentContractLines\" WHERE \"Id\"='$LA') AND \"Status\"='Booked' AND \"StudentContractLineId\" IS NULL;")"
expectn "LA scheduled=4" 4 "$(linesched "$SA" "$LA")"
expectn "LA available=0 (4-4)" 0 "$(lineavail "$SA" "$LA")"

echo "--- 2) lessonCount aşımı → 409 (hak kalmadı) ---"
S1=$(mksession "$I1P" "$SOON" "12:00")
expect "available 0 line booking → 409" 409 "$(bookcode "$S1" "$SA")"

echo "--- 3) Regular ders line tarih aralığı dışında → 409 (uygun line yok) ---"
SB=$(guest SB); LB=$(addline "$SB" "$I1P" 4 false | jget id)
SOUT=$(mksession "$I1P" "$FAR" "18:00")  # FAR > END (line aralığı dışında)
expect "tarih-dışı booking → 409" 409 "$(bookcode "$SOUT" "$SB")"

echo "--- 4) Regular reschedule line.EndDate dışına → 409 ---"
SESS_A=$(sql "SELECT s.\"Id\" FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LA' AND s.\"Status\"='Scheduled' ORDER BY s.\"StartAt\" LIMIT 1;")
expect "reschedule → FAR (END sonrası) → 409" 409 "$(pcode "/api/studio/class-sessions/$SESS_A/reschedule" "{\"newStartAt\":\"${FAR}T18:00:00\",\"keepBookings\":true,\"reason\":\"t\"}" "$ATOK")"

echo "--- 5) Regular chain-shift line.EndDate dışına → 409 ---"
expect "chain-shift → FAR → 409" 409 "$(pcode "/api/studio/class-sessions/$SESS_A/chain-shift" "{\"newStartAt\":\"${FAR}T18:00:00\"}" "$ATOK")"

echo "--- 6) Bireysel line grup dersine atanamaz → 409 ---"
SC=$(guest SC); LC=$(addline "$SC" "$I1P" 4 true | jget id)  # isIndividual=true
SGRP=$(mksession "$I1P" "$SOON" "13:00")  # manuel = grup (IsIndividual=false)
RIND=$(curl -s -w "\n%{http_code}" -X POST "$BASE/api/studio/class-sessions/$SGRP/bookings" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"studentProfileId\":\"$SC\"}")
expect "bireysel line grup dersine → 409" 409 "$(code "$RIND")"
expect "mesaj 'Bireysel' içerir" yes "$(has "$(body "$RIND")" "Bireysel")"

echo "--- 7) Bireysel line generate → IsIndividual seans + 2. öğrenci eklenemez + gruba konmaz ---"
RGC=$(genline "$LC"); expect "bireysel generate → 200" 200 "$(code "$RGC")"
expectn "LC tüm seansları IsIndividual=true (grup=0)" 0 "$(sql "SELECT count(*) FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LC' AND s.\"IsIndividual\"=false;")"
SESS_I=$(sql "SELECT s.\"Id\" FROM \"ClassSessions\" s JOIN \"ClassBookings\" b ON b.\"ClassSessionId\"=s.\"Id\" WHERE b.\"StudentContractLineId\"='$LC' AND s.\"Status\"='Scheduled' ORDER BY s.\"StartAt\" LIMIT 1;")
expect "bireysel seansa 2. öğrenci → 409" 409 "$(bookcode "$SESS_I" "$SB")"

echo "--- 8) Group schedule change lineId ile çalışır ---"
CAND=$(gbody "/api/studio/group-schedule-change/candidates?instructorProfileId=$I1P&classTypeId=$CT&effectiveDate=$TODAY" "$ATOK")
expect "candidates lineId alanı döner" yes "$(has "$CAND" '"lineId"')"
expect "candidates LA'yı içerir" yes "$(has "$CAND" "$LA")"
PRV=$(post "/api/studio/group-schedule-change/preview" "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$LA\"],\"newSlots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"20:00:00\"}]}" "$ATOK")
expect "preview (lineIds) → 200" 200 "$(code "$PRV")"
expect "preview students[].lineId döner" yes "$(has "$(body "$PRV")" '"lineId"')"

echo "--- 9) Student level filter ---"
SL=$(guest SL Beginner)
expect "level=Beginner listesi SL içerir" yes "$(has "$(gbody "/api/studio/students?level=Beginner" "$ATOK")" "$SL")"
expect "level=Advanced listesi SL İÇERMEZ" no "$(has "$(gbody "/api/studio/students?level=Advanced" "$ATOK")" "$SL")"

echo "--- 10) Legacy null-line booking fallback: tek-line header SAYAR ---"
# SA header'ı tek-line (yalnız LA). Bir booking'i lineId=NULL yap → fallback hâlâ sayar → available 0 kalır.
ONEBK=$(sql "SELECT \"Id\" FROM \"ClassBookings\" WHERE \"StudentContractLineId\"='$LA' AND \"Status\"='Booked' LIMIT 1;")
sql "UPDATE \"ClassBookings\" SET \"StudentContractLineId\"=NULL WHERE \"Id\"='$ONEBK';" >/dev/null
expectn "tek-line header: null-line booking fallback ile sayılır (available 0)" 0 "$(lineavail "$SA" "$LA")"

echo "--- 11) Çok-line header'da null-line booking hiçbir line'a sayılmaz ---"
SM=$(guest SM); LM1=$(addline "$SM" "$I1P" 4 false | jget id)
LM2=$(addline "$SM" "$I1P" 4 false | jget id)  # aynı header (en güncel aktif) → 2-line header
HM=$(sql "SELECT \"StudentContractId\" FROM \"StudentContractLines\" WHERE \"Id\"='$LM1';")
expectn "HM 2 line" 2 "$(sql "SELECT count(*) FROM \"StudentContractLines\" WHERE \"StudentContractId\"='$HM';")"
genline "$LM1" >/dev/null
expectn "LM1 available 0 (4 scheduled)" 0 "$(lineavail "$SM" "$LM1")"
M1BK=$(sql "SELECT \"Id\" FROM \"ClassBookings\" WHERE \"StudentContractLineId\"='$LM1' AND \"Status\"='Booked' LIMIT 1;")
sql "UPDATE \"ClassBookings\" SET \"StudentContractLineId\"=NULL WHERE \"Id\"='$M1BK';" >/dev/null
expectn "çok-line header: null-line booking SAYILMAZ → LM1 available 1" 1 "$(lineavail "$SM" "$LM1")"
expectn "LM2 etkilenmez (available 4)" 4 "$(lineavail "$SM" "$LM2")"

echo "--- 12) Regresyon: v1.4.0 line payment/debt ---"
PAY=$(body "$(post "/api/studio/students/$SB/payments" "{\"amount\":200,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$LB\"}" "$ATOK")" | jget id)
pcode "/api/studio/payments/$PAY/mark-paid" '{}' "$ATOK" >/dev/null
expectn "LB paid=200" 200 "$(gbody "/api/studio/students/$SB/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LB')?.paid")"

echo "--- 13) Regresyon: makeup request akışı (28C-2 line-based) çalışır ---"
pcode "/api/studio/students/$SB/makeup-credits/grant" "{\"studentContractLineId\":\"$LB\",\"amount\":1,\"notes\":\"smoke\"}" "$ATOK" >/dev/null
FUT=$(date -d "+5 days" +%F)
expect "makeup request create (line-based) → 200" 200 "$(pcode "/api/studio/makeup-requests" "{\"studentContractLineId\":\"$LB\",\"startAt\":\"${FUT}T12:00:00\"}" "$ATOK")"

echo "--- 14) Regresyon: v1.1.2 rol hardening ---"
expect "Instructor borç görüntüleme → 403" 403 "$(gcode "/api/studio/students/$SA/debt" "$I1TOK")"
expect "Instructor line generate → 404/403 (Owner/Admin ucu)" 403 "$(pcode "/api/studio/contract-lines/$LA/schedule/generate" "{\"slots\":$ALLDAYS,\"reuseExistingGroup\":false}" "$I1TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 28C-1 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 28C-1 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
