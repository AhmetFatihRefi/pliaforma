#!/usr/bin/env bash
# Pliaforma — Sprint 20 smoke (Makeup Flow, Studio Closure & Instructor-Caused Makeup):
#  - Eligible-students: hak yoksa listede yok; grant sonrası var.  Strict: kredisiz Makeup booking → 409 (Owner dâhil).
#  - Kredili Makeup booking → 200 + Use(-1); iptal → Restore(+1).  Makeup booking sözleşme hakkını düşürmez (ContractId NULL).
#  - Telafi dersi ekleme yalnız Owner/StudioAdmin (Instructor → 403).
#  - NotDelivered → Cancelled + +1 telafi (MakeupAllowance bağımsız); idempotent.  Reschedule/ActualInstructor → telafi YOK.
#  - Closure CancelAndGrantMakeupCredit → +1 (HolidayClosure), idempotent; RescheduleByOffset → telafi YOK.
#  - AbsentEarned (Sprint 19) ↔ NotDelivered/Closure çakışmaz.  Instructor closure → 403.  Student makeup salt-okunur.
# Postgres container gerekli. Kullanım: scripts/sprint20-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":[0-9-]*" | head -1 | sed 's/.*://'; }
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
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$2', \"EndAt\"='$2'::timestamp + interval '1 hour' WHERE \"Id\"='$1';" >/dev/null; }
book(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "${3:-$ATOK}")"; }
# Sprint 28C-2: grant LINE-scoped → $1=student $2=lineId $3=amount. lineof: contract → line.
lineof(){ sql "SELECT \"Id\" FROM \"StudentContractLines\" WHERE \"StudentContractId\"='$1' LIMIT 1;"; }
grant(){ post "/api/studio/students/$1/makeup-credits/grant" "{\"studentContractLineId\":\"$2\",\"amount\":$3,\"notes\":\"smoke\"}" "$ATOK" >/dev/null; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S20 $TS\",\"ownerFullName\":\"O\",\"email\":\"o20+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S20 $TS';")
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
S1=$(guest Stu1); S2=$(guest Stu2); S3=$(guest Stu3); S4=$(guest Stu4)
mkcontract(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":50,\"makeupAllowance\":$2,\"price\":0,\"isIndividualDefault\":false}" "$ATOK")" | jget id; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\",\"sessionKind\":\"${3:-Regular}\"}" "$ATOK")" | jget id; }
C1=$(mkcontract "$S1" 5); C2=$(mkcontract "$S2" 0); C4=$(mkcontract "$S4" 5)   # C2 allowance=0 → NotDelivered bağımsızlığı
FUT=$(date -d "+3 days" +%Y-%m-%dT09:00:00)

echo "--- 1) Eligible-students + strict eligibility ---"
EL0=$(curl -s "$BASE/api/studio/makeup-eligible-students" -H "Authorization: Bearer $ATOK")
expect "kredisiz S3 eligible listede YOK" no "$(has "$EL0" "$S3")"
grant "$S1" "$(lineof "$C1")" 2
EL1=$(curl -s "$BASE/api/studio/makeup-eligible-students" -H "Authorization: Bearer $ATOK")
expect "grant sonrası S1 eligible listede VAR" yes "$(has "$EL1" "$S1")"
MS1=$(mksess "$I1P" "$FUT" Makeup)
expect "kredisiz S3 Makeup booking → 409 (Owner bile)" 409 "$(pcode "/api/studio/class-sessions/$MS1/bookings" "{\"studentProfileId\":\"$S3\"}" "$ATOK")"

echo "--- 2) Makeup booking Use(-1) + iptal Restore(+1) + sözleşme hakkı düşmez ---"
MBRAW=$(post "/api/studio/class-sessions/$MS1/bookings" "{\"studentProfileId\":\"$S1\"}" "$ATOK"); MBID=$(body "$MBRAW" | jget id)
expect "kredili S1 Makeup booking → 200" 200 "$(code "$MBRAW")"
expect "Makeup booking sonrası bakiye 1 (Use -1)" 1 "$(bal "$S1")"
# Sprint 28C-2: makeup booking artık LINE'a bağlı (StudentContractLineId dolu); Regular ders hakkı düşmez (SessionKind=Makeup sayılmaz).
expect "Makeup booking line'a bağlı (StudentContractLineId dolu)" 1 "$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"Id\"='$MBID' AND \"StudentContractLineId\" IS NOT NULL;")"
expect "Makeup booking SessionKind=Makeup (regular hakkı düşmez)" 1 "$(sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"Id\"='$MBID' AND s.\"SessionKind\"='Makeup';")"
expect "Makeup booking iptal → 200" 200 "$(pcode "/api/studio/bookings/$MBID/cancel" '{}' "$ATOK")"
expect "iptal sonrası bakiye 2 (Restore +1)" 2 "$(bal "$S1")"

echo "--- 3) Telafi dersi ekleme yalnız Owner/StudioAdmin ---"
MS_I2=$(mksess "$I2P" "$(date -d "+3 days" +%Y-%m-%dT10:00:00)" Makeup)
expect "Instructor Makeup booking → 403" 403 "$(pcode "/api/studio/class-sessions/$MS_I2/bookings" "{\"studentProfileId\":\"$S1\"}" "$I2TOK")"

echo "--- 4) NotDelivered → +1 telafi (MakeupAllowance bağımsız) + idempotent ---"
RS1=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT11:00:00)"); book "$RS1" "$S2" >/dev/null
expect "S2 başlangıç bakiye 0" 0 "$(bal "$S2")"
expect "not-delivered → 200" 200 "$(pcode "/api/studio/class-sessions/$RS1/not-delivered" '{}' "$ATOK")"
expect "not-delivered sonrası RS1 Cancelled" Cancelled "$(sql "SELECT \"Status\" FROM \"ClassSessions\" WHERE \"Id\"='$RS1';")"
expect "S2 +1 telafi (allowance=0 olsa bile — bağımsız)" 1 "$(bal "$S2")"
expect "idempotent: tekrar not-delivered → 409 (zaten iptal)" 409 "$(pcode "/api/studio/class-sessions/$RS1/not-delivered" '{}' "$ATOK")"
expect "tekrar sonrası bakiye hâlâ 1" 1 "$(bal "$S2")"

echo "--- 5) Reschedule + ActualInstructor → telafi YOK ---"
RS2=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT12:00:00)"); book "$RS2" "$S4" >/dev/null
pcode "/api/studio/class-sessions/$RS2/reschedule" "{\"newStartAt\":\"$(date -d "+4 days" +%Y-%m-%dT12:00:00)\",\"keepBookings\":true}" "$ATOK" >/dev/null
expect "reschedule (keepBookings) sonrası S4 bakiye 0 (telafi yok)" 0 "$(bal "$S4")"
RS3=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT13:00:00)"); book "$RS3" "$S4" >/dev/null
pcode "/api/studio/class-sessions/$RS3/actual-instructor" "{\"actualInstructorProfileId\":\"$I2P\"}" "$ATOK" >/dev/null
expect "actual-instructor sonrası S4 bakiye 0 (telafi yok)" 0 "$(bal "$S4")"

echo "--- 6) Closure → +1 (HolidayClosure), idempotent; RescheduleByOffset → telafi YOK ---"
DCL=$(date -d "+5 days" +%Y-%m-%d)
RS_C=$(mksess "$I1P" "${DCL}T10:00:00"); book "$RS_C" "$S2" >/dev/null
CLR=$(post /api/studio/closures "{\"title\":\"Tatil $TS\",\"startAt\":\"${DCL}T00:00:00\",\"endAt\":\"${DCL}T23:59:00\",\"actionType\":\"CancelAndGrantMakeupCredit\"}" "$ATOK")
expect "closure → 200" 200 "$(code "$CLR")"
expect "closure creditedCount=1" 1 "$(body "$CLR" | jnum creditedCount)"
expect "S2 closure sonrası +1 (bakiye 2: not-delivered 1 + closure 1)" 2 "$(bal "$S2")"
CLR2=$(post /api/studio/closures "{\"title\":\"Tatil2 $TS\",\"startAt\":\"${DCL}T00:00:00\",\"endAt\":\"${DCL}T23:59:00\",\"actionType\":\"CancelAndGrantMakeupCredit\"}" "$ATOK")
expect "idempotent: aynı aralık tekrar closure creditedCount=0" 0 "$(body "$CLR2" | jnum creditedCount)"
DRB=$(date -d "+6 days" +%Y-%m-%d)
RS_R=$(mksess "$I1P" "${DRB}T10:00:00"); book "$RS_R" "$S4" >/dev/null
post /api/studio/closures "{\"title\":\"Ertele $TS\",\"startAt\":\"${DRB}T00:00:00\",\"endAt\":\"${DRB}T23:59:00\",\"actionType\":\"RescheduleByOffset\",\"offsetDays\":7}" "$ATOK" >/dev/null
expect "RescheduleByOffset sonrası S4 bakiye 0 (telafi yok)" 0 "$(bal "$S4")"

echo "--- 7) AbsentEarned (Sprint 19) ↔ closure/not-delivered çakışmaz ---"
# S4: backdate Regular Absent (+1 AbsentEarned) + ayrı derste not-delivered (+1) → bakiye 2, çakışma yok.
PS_A=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT14:00:00)"); BA=$(book "$PS_A" "$S4" | jget id); backdate "$PS_A" "2024-02-10 10:00:00"
curl -s -o /dev/null -X PUT "$BASE/api/studio/class-sessions/$PS_A/attendance" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Absent\"}]}"
expect "S4 Absent → AbsentEarned +1 (bakiye 1)" 1 "$(bal "$S4")"
RS_N=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT15:00:00)"); book "$RS_N" "$S4" >/dev/null
pcode "/api/studio/class-sessions/$RS_N/not-delivered" '{}' "$ATOK" >/dev/null
expect "S4 not-delivered +1 → bakiye 2 (iki kaynak bir arada)" 2 "$(bal "$S4")"

echo "--- 8) Yetkiler + öğrenci salt-okunur ---"
expect "Instructor not-delivered → 403" 403 "$(pcode "/api/studio/class-sessions/$RS3/not-delivered" '{}' "$I2TOK")"
expect "Instructor closure → 403" 403 "$(pcode /api/studio/closures "{\"title\":\"x\",\"startAt\":\"${DCL}T00:00:00\",\"endAt\":\"${DCL}T23:59:00\",\"actionType\":\"CancelSessions\"}" "$I2TOK")"
expect "Instructor eligible-students → 403" 403 "$(gcode /api/studio/makeup-eligible-students "$I2TOK")"
# Öğrenci self: kendi telafi kredisini salt-okunur görür (yazma ucu yok).
SUE="su20+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"SU\",\"email\":\"$SUE\",\"password\":\"$PW\"}" >/dev/null; vrf "$SUE"
post "/api/studio/students/$S1/convert-to-user" "{\"email\":\"$SUE\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
SUTOK=$(login "$SUE")
expect "öğrenci kendi telafi kredisi (salt-okunur) → 200" 200 "$(gcode /api/student/makeup-credits "$SUTOK")"
expect "öğrenci eligible-students (staff ucu) → 403" 403 "$(gcode /api/studio/makeup-eligible-students "$SUTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 20 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 20 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
