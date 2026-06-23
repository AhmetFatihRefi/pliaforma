#!/usr/bin/env bash
# Pliaforma — Sprint 25 smoke (Pilot Readiness / QA Hardening — pilot-gate):
#  YENİ FIX (Sprint 25):
#   - ConfirmSession: Instructor yalnız PLANLANAN veya FİİLÎ hocası kendisi olan dersi onaylar (başka → 403).
#   - ConfirmToday: yalnız Owner/StudioAdmin (Instructor → 403).
#  REGRESYON (pilot-gate): Instructor finans/closure/group-change/not-delivered 403; student staff ucu 403;
#   student kendi borcu 200 + multi-currency AYRI (tek toplam yok); not-delivered → telafi; reschedule → telafi yok;
#   amendment → program üretmez.  (group-change→telafi yok derin akışı sprint22-smoke'ta; aynı suite'te koşar.)
# Postgres container gerekli. Kullanım: scripts/sprint25-smoke.sh [BASE]
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
get(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ get /api/studio/memberships "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
bal(){ sql "SELECT COALESCE(SUM(\"Amount\"),0) FROM \"MakeupCreditTransactions\" WHERE \"StudioId\"='$SID' AND \"StudentProfileId\"='$1';"; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$2', \"EndAt\"='$2'::timestamp + interval '1 hour' WHERE \"Id\"='$1';" >/dev/null; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S25 $TS\",\"ownerFullName\":\"O\",\"email\":\"o25+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S25 $TS';")
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2); I1TOK=$(login "i1+$TS@pf.local"); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$2\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":50,\"makeupAllowance\":$3,\"price\":$4,\"currency\":\"$5\",\"isIndividualDefault\":false}" "$ATOK")" | jget id; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\"}" "$ATOK")" | jget id; }
book(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }
mkstudent(){ local sp="$1" e="$2"; post /api/auth/register "{\"fullName\":\"$3\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; post "/api/studio/students/$sp/convert-to-user" "{\"email\":\"$e\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; login "$e"; }
S1=$(guest Stu1); S2=$(guest Stu2)
C1=$(mkc "$S1" "$I1P" 1 0 TRY); C2=$(mkc "$S2" "$I1P" 0 0 TRY)
D3=$(date -d "+3 days" +%Y-%m-%d); TODAY=$(date +%Y-%m-%d)
# Her seansa BENZERSİZ saat (aynı hocada çakışma olmasın). Owner-yapımı seanslar; gerekirse geçmişe çekilir.

echo "--- 1) Confirm scope (Sprint 25 fix): Instructor yalnız kendi dersini onaylar ---"
PO=$(mksess "$I1P" "${D3}T10:00:00"); book "$PO" "$S1" >/dev/null; backdate "$PO" "2024-03-04 10:00:00"
expect "Instructor I1 KENDİ (planlanan) dersini onayla → 200" 200 "$(pcode "/api/studio/class-sessions/$PO/confirm" '{}' "$I1TOK")"
PX=$(mksess "$I2P" "${D3}T11:00:00"); backdate "$PX" "2024-03-04 11:00:00"
expect "Instructor I1 BAŞKA hocanın (I2) dersini onayla → 403" 403 "$(pcode "/api/studio/class-sessions/$PX/confirm" '{}' "$I1TOK")"

echo "--- 2) Confirm scope: FİİLÎ hoca da onaylayabilir ---"
PA=$(mksess "$I2P" "${D3}T12:00:00"); backdate "$PA" "2024-03-04 12:00:00"
pcode "/api/studio/class-sessions/$PA/actual-instructor" "{\"actualInstructorProfileId\":\"$I1P\"}" "$ATOK" >/dev/null
expect "Instructor I1 FİİLÎ hocası olduğu dersi onayla → 200" 200 "$(pcode "/api/studio/class-sessions/$PA/confirm" '{}' "$I1TOK")"

echo "--- 3) ConfirmToday: yalnız Owner/StudioAdmin ---"
PT=$(mksess "$I1P" "${D3}T13:00:00"); book "$PT" "$S1" >/dev/null; backdate "$PT" "$TODAY 00:30:00"
expect "Instructor confirm-today → 403" 403 "$(pcode /api/studio/class-sessions/confirm-today '{}' "$I1TOK")"
expect "Owner confirm-today → 200" 200 "$(pcode /api/studio/class-sessions/confirm-today '{}' "$ATOK")"

echo "--- 4) Instructor yetki regresyonu (finans/closure/group-change/not-delivered → 403) ---"
expect "Instructor payments → 403" 403 "$(gcode /api/studio/payments "$I1TOK")"
expect "Instructor closure oluştur → 403" 403 "$(pcode /api/studio/closures "{\"title\":\"x\",\"startAt\":\"${TODAY}T00:00:00\",\"endAt\":\"${TODAY}T23:59:00\",\"actionType\":\"CancelSessions\"}" "$I1TOK")"
expect "Instructor group-change candidates → 403" 403 "$(gcode "/api/studio/group-schedule-change/candidates?instructorProfileId=$I1P&classTypeId=$CT&effectiveDate=$TODAY" "$I1TOK")"
PND=$(mksess "$I1P" "${D3}T14:00:00"); book "$PND" "$S1" >/dev/null
expect "Instructor not-delivered → 403" 403 "$(pcode "/api/studio/class-sessions/$PND/not-delivered" '{}' "$I1TOK")"

echo "--- 5) İş kuralı regresyonu: not-delivered telafi VAR; reschedule telafi YOK ---"
PNS=$(mksess "$I1P" "${D3}T15:00:00"); book "$PNS" "$S2" >/dev/null
expect "not-delivered → 200" 200 "$(pcode "/api/studio/class-sessions/$PNS/not-delivered" '{}' "$ATOK")"
expect "not-delivered → S2 telafi +1 (allowance 0 olsa bile)" 1 "$(bal "$S2")"
PR=$(mksess "$I1P" "${D3}T16:00:00"); book "$PR" "$S1" >/dev/null
pcode "/api/studio/class-sessions/$PR/reschedule" "{\"newStartAt\":\"${D3}T18:00:00\",\"keepBookings\":true}" "$ATOK" >/dev/null
expect "reschedule → S1 telafi YOK (bakiye 0)" 0 "$(bal "$S1")"

echo "--- 6) Amendment program ÜRETMEZ ---"
SCHED_BEFORE=$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"StudentContractId\"='$C1' AND \"Status\"='Booked' AND \"ClassSessionId\" IN (SELECT \"Id\" FROM \"ClassSessions\" WHERE \"SessionKind\"='Regular');")
post "/api/studio/contracts/$C1/amendments" "{\"kind\":\"ExtraLessons\",\"additionalLessonCount\":4,\"additionalPrice\":1000}" "$ATOK" >/dev/null
SCHED_AFTER=$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"StudentContractId\"='$C1' AND \"Status\"='Booked' AND \"ClassSessionId\" IN (SELECT \"Id\" FROM \"ClassSessions\" WHERE \"SessionKind\"='Regular');")
expect "amendment sonrası Regular booking sayısı DEĞİŞMEDİ (program üretmez)" "$SCHED_BEFORE" "$SCHED_AFTER"

echo "--- 7) Student izolasyon + multi-currency (AYRI, tek toplam yok) ---"
SD=$(guest StuD); CD_TRY=$(mkc "$SD" "$I1P" 0 5000 TRY); CD_USD=$(mkc "$SD" "$I1P" 0 100 USD)
SDTOK=$(mkstudent "$SD" "sd25+$TS@pf.local" "StuD")
expect "Student staff öğrenci listesi → 403" 403 "$(gcode /api/studio/students "$SDTOK")"
DBT=$(get /api/student/debt "$SDTOK")
expect "Student kendi borcu → 200" 200 "$(gcode /api/student/debt "$SDTOK")"
expect "Student debt TRY bloğu var" yes "$(has "$DBT" '"currency":"TRY"')"
expect "Student debt USD bloğu AYRI var" yes "$(has "$DBT" '"currency":"USD"')"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 25 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 25 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
