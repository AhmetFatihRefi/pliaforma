#!/usr/bin/env bash
# Pliaforma — Sprint 22 smoke (Group Schedule Change & Student Reassignment):
#  - Candidates: hoca+ders tipinde gelecek Regular dersi olan sözleşmeler listelenir.
#  - Preview = DRY-RUN (DB değişmez); apply ile birebir (reuse/kaldırma/üretim sayıları).
#  - Apply: seçili öğrencilerin gelecek Regular bookingleri TELAFİSİZ kaldırılır; yeni slotlardan üretilir;
#    2 öğrenci aynı yeni slotta TEK grup seansı reuse eder; seçilmeyen öğrenci dokunulmaz; boş eski session Cancelled.
#  - Telafi ledger değişmez. Atomik abort (çakışmada hiçbir şey değişmez). Yetki: Instructor → 403.
# Postgres container gerekli. Kullanım: scripts/sprint22-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
bal(){ sql "SELECT COALESCE(SUM(\"Amount\"),0) FROM \"MakeupCreditTransactions\" WHERE \"StudioId\"='$SID' AND \"StudentProfileId\"='$1';"; }
booked(){ sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"StudentProfileId\"='$1' AND \"Status\"='Booked';"; }
bookedDow(){ sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentProfileId\"='$1' AND b.\"Status\"='Booked' AND EXTRACT(DOW FROM s.\"StartAt\")=$2;"; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S22 $TS\",\"ownerFullName\":\"O\",\"email\":\"o22+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S22 $TS';")
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2TOK=$(login "i1+$TS@pf.local")   # i1 instructor token (kendi yetkisi testi)
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
ED=$(date -d "+60 days" +%F)   # contract end
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$(date +%F)\",\"endDate\":\"$ED\",\"totalLessonCount\":8,\"makeupAllowance\":2,\"price\":0,\"isIndividualDefault\":$2}" "$ATOK")" | jget id; }
genc(){ pcode "/api/studio/contracts/$1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"$2\",\"startTime\":\"$3:00\"}],\"reuseExistingGroup\":true,\"individual\":$4}" "$ATOK"; }
# Sprint 28C-1: grup düzeni değişikliği LINE-bazlı → contract'ın line id'si (28A dual-write: 1 contract = 1 line).
lineof(){ sql "SELECT \"Id\" FROM \"StudentContractLines\" WHERE \"StudentContractId\"='$1' LIMIT 1;"; }
TODAY=$(date +%F)
St1=$(guest St1); St2=$(guest St2); St3=$(guest St3); St4=$(guest St4); Sob=$(guest Sob)
C1=$(mkc "$St1" false); C2=$(mkc "$St2" false); C3=$(mkc "$St3" false); C4=$(mkc "$St4" false); COB=$(mkc "$Sob" true)
# Eski düzen: St1,St2,St3 grup Pazartesi 18:00; St4 grup Çarşamba 18:00 (yalnız); Sob BİREYSEL Salı 19:00 (engel).
genc "$C1" Monday 18:00 null >/dev/null; genc "$C2" Monday 18:00 null >/dev/null; genc "$C3" Monday 18:00 null >/dev/null
genc "$C4" Wednesday 18:00 null >/dev/null
genc "$COB" Tuesday 19:00 true >/dev/null
NEWSLOTS_FRI='[{"dayOfWeek":"Friday","startTime":"19:00:00"}]'
NEWSLOTS_SAT='[{"dayOfWeek":"Saturday","startTime":"19:00:00"}]'
NEWSLOTS_TUE='[{"dayOfWeek":"Tuesday","startTime":"19:00:00"}]'

echo "--- 1) Candidates ---"
CAND=$(curl -s "$BASE/api/studio/group-schedule-change/candidates?instructorProfileId=$I1P&classTypeId=$CT&effectiveDate=$TODAY" -H "Authorization: Bearer $ATOK")
expect "candidates St1 listede" yes "$(has "$CAND" "$St1")"
expect "candidates St4 listede" yes "$(has "$CAND" "$St4")"

echo "--- 2) Preview = DRY-RUN (DB değişmez) ---"
St1MonBefore=$(bookedDow "$St1" 1)
PV=$(post /api/studio/group-schedule-change/preview "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$(lineof $C1)\",\"$(lineof $C2)\"],\"newSlots\":$NEWSLOTS_FRI}" "$ATOK")
expect "preview → 200" 200 "$(code "$PV")"
expect "preview canApply true" yes "$(has "$(body "$PV")" '"canApply":true')"
expect "preview reuse var (St2 St1'in slotunu paylaşır)" yes "$(echo "$(body "$PV")" | grep -o '"reusedCount":[1-9]' >/dev/null && echo yes || echo no)"
expect "DRY-RUN: St1 Pazartesi bookingleri DEĞİŞMEDİ" "$St1MonBefore" "$(bookedDow "$St1" 1)"

echo "--- 3) Apply (St1,St2 → Cuma 19:00) ---"
expect "apply → 200" 200 "$(pcode /api/studio/group-schedule-change/apply "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$(lineof $C1)\",\"$(lineof $C2)\"],\"newSlots\":$NEWSLOTS_FRI}" "$ATOK")"
expect "St1 Pazartesi (eski) booking kalmadı" 0 "$(bookedDow "$St1" 1)"
expect "St1 Cuma (yeni) bookingleri var" yes "$([ "$(bookedDow "$St1" 5)" -ge 1 ] && echo yes || echo no)"
expect "cross-student reuse: bir Cuma grup seansı BookedCount=2" 2 "$(sql "SELECT COALESCE(MAX(\"BookedCount\"),0) FROM \"ClassSessions\" WHERE \"InstructorProfileId\"='$I1P' AND \"Status\"='Scheduled' AND EXTRACT(DOW FROM \"StartAt\")=5;")"
expect "seçilmeyen St3 Pazartesi dersleri DURUYOR" yes "$([ "$(bookedDow "$St3" 1)" -ge 1 ] && echo yes || echo no)"
expect "telafi üretilmedi (St1 bakiye 0)" 0 "$(bal "$St1")"
expect "St1,St2 ayrıldı ama St3 kaldı → Pazartesi session boşalmadı (Scheduled)" yes "$([ "$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"InstructorProfileId\"='$I1P' AND \"Status\"='Scheduled' AND EXTRACT(DOW FROM \"StartAt\")=1 AND \"BookedCount\">0;")" -ge 1 ] && echo yes || echo no)"

echo "--- 4) Boş kalan eski session → Cancelled (St4 yalnız Çarşamba) ---"
pcode /api/studio/group-schedule-change/apply "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$(lineof $C4)\"],\"newSlots\":$NEWSLOTS_SAT}" "$ATOK" >/dev/null
expect "St4 eski Çarşamba sessionları boşaldı → Cancelled" yes "$([ "$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"InstructorProfileId\"='$I1P' AND \"Status\"='Cancelled' AND EXTRACT(DOW FROM \"StartAt\")=3;")" -ge 1 ] && echo yes || echo no)"
expect "St4 telafi üretilmedi (bakiye 0)" 0 "$(bal "$St4")"

echo "--- 5) Atomik abort: çakışmada hiçbir şey değişmez (St3 → Salı 19:00, Sob bireysel engeli) ---"
PVC=$(post /api/studio/group-schedule-change/preview "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$(lineof $C3)\"],\"newSlots\":$NEWSLOTS_TUE}" "$ATOK")
expect "preview canApply false (hoca çakışması — Sob bireysel Salı 19:00)" no "$(has "$(body "$PVC")" '"canApply":true')"
expect "preview conflict işaretli" yes "$(has "$(body "$PVC")" '"conflict"')"
St3MonBefore=$(bookedDow "$St3" 1)
expect "apply → 409 (atomik)" 409 "$(pcode /api/studio/group-schedule-change/apply "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$(lineof $C3)\"],\"newSlots\":$NEWSLOTS_TUE}" "$ATOK")"
expect "ATOMİK: St3 Pazartesi bookingleri DEĞİŞMEDİ" "$St3MonBefore" "$(bookedDow "$St3" 1)"
expect "ATOMİK: St3 Salı dersi oluşmadı" 0 "$(bookedDow "$St3" 2)"

echo "--- 6) Yetki ---"
expect "Instructor candidates → 403" 403 "$(gcode "/api/studio/group-schedule-change/candidates?instructorProfileId=$I1P&classTypeId=$CT&effectiveDate=$TODAY" "$I2TOK")"
expect "Instructor apply → 403" 403 "$(pcode /api/studio/group-schedule-change/apply "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$TODAY\",\"lineIds\":[\"$(lineof $C3)\"],\"newSlots\":$NEWSLOTS_SAT}" "$I2TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 22 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 22 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
