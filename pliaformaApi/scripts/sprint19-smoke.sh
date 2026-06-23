#!/usr/bin/env bash
# Pliaforma — Sprint 19 smoke (Attendance Confirmation, Actual Instructor & Instructor Report):
#  - Present yoklama sözleşme dersini tamamlar; PAKET consume çağrılmaz (StudentPackageTransaction yok).
#  - Absent → sözleşme MakeupAllowance limitli telafi; aşımda yeni telafi yok; aynı ders çift telafi yok.
#  - Absent→Present telafiyi geri alır (immutable ledger); tekrar Absent → restore.
#  - ConfirmSession: gelecek ders 409; başlamış Scheduled → Completed + işaretlenmemiş Present; idempotent.
#  - ConfirmTodaySessions çalışır.  SetActualInstructor → rapor fiilî hocaya atfeder; null → planlanan.
#  - Hoca raporu PARASIZ (estimatedEarning yok).  Yetki: instructor başka ders 403; actual-instructor yalnız Owner/Admin.
# Postgres container gerekli. Kullanım: scripts/sprint19-smoke.sh [BASE]
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
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
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
mark(){ putcode "/api/studio/class-sessions/$1/attendance" "{\"items\":[{\"bookingId\":\"$2\",\"status\":\"$3\"}]}" "$ATOK"; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S19 $TS\",\"ownerFullName\":\"O\",\"email\":\"o19+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S19 $TS';")
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
S1=$(guest Stu1); S2=$(guest Stu2)
mkcontract(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":50,\"makeupAllowance\":$2,\"price\":0,\"isIndividualDefault\":false}" "$ATOK")" | jget id; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\"}" "$ATOK")" | jget id; }
book(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }
C1=$(mkcontract "$S1" 1); C2=$(mkcontract "$S2" 1)   # MakeupAllowance=1 (cap testleri için)
FUT=$(date -d "+3 days" +%Y-%m-%dT10:00:00); PAST="2024-03-04 10:00:00"; PAST2="2024-03-05 10:00:00"; TODAY=$(date +%Y-%m-%d)

echo "--- 1) Present sözleşme dersini tamamlar; paket consume YOK ---"
PS1=$(mksess "$I1P" "$FUT"); B1=$(book "$PS1" "$S1"); backdate "$PS1" "$PAST"
expect "Present yoklama → 200" 200 "$(mark "$PS1" "$B1" Present)"
expect "Present sonrası bakiye 0 (telafi yok)" 0 "$(bal "$S1")"
expect "PAKET consume çağrılmadı (hiç StudentPackageTransaction yok)" 0 "$(sql "SELECT count(*) FROM \"StudentPackageTransactions\" WHERE \"StudioId\"='$SID';")"

echo "--- 2) Absent → telafi (+1); aynı ders çift telafi yok ---"
PS2=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT11:00:00)"); B2=$(book "$PS2" "$S1"); backdate "$PS2" "$PAST2"
expect "Absent yoklama → 200" 200 "$(mark "$PS2" "$B2" Absent)"
expect "Absent sonrası bakiye 1" 1 "$(bal "$S1")"
mark "$PS2" "$B2" Absent >/dev/null
expect "aynı ders 2. kez Absent → çift telafi yok (bakiye 1)" 1 "$(bal "$S1")"

echo "--- 3) Absent→Present geri alır; tekrar Absent → restore ---"
expect "Absent→Present → 200" 200 "$(mark "$PS2" "$B2" Present)"
expect "geri alma sonrası bakiye 0" 0 "$(bal "$S1")"
mark "$PS2" "$B2" Absent >/dev/null
expect "tekrar Absent → restore (bakiye 1)" 1 "$(bal "$S1")"

echo "--- 4) Sözleşme MakeupAllowance limiti (1) ---"
# S1 zaten 1 telafi (PS2). 2. farklı derste Absent → limit dolu, yeni telafi yok.
PS3=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT12:00:00)"); B3=$(book "$PS3" "$S1"); backdate "$PS3" "2024-03-06 10:00:00"
mark "$PS3" "$B3" Absent >/dev/null
expect "limit dolu → 2. devamsızlık telafi üretmez (bakiye hâlâ 1)" 1 "$(bal "$S1")"

echo "--- 5) Ders onayı: gelecek 409; başlamış Scheduled → Completed + Present; idempotent ---"
PSF=$(mksess "$I1P" "$FUT")   # gelecek (backdate YOK)
expect "gelecek dersi onayla → 409" 409 "$(pcode "/api/studio/class-sessions/$PSF/confirm" '{}' "$ATOK")"
PSC=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT13:00:00)"); BC=$(book "$PSC" "$S2"); backdate "$PSC" "2024-03-07 10:00:00"
expect "başlamış Scheduled dersi onayla → 200" 200 "$(pcode "/api/studio/class-sessions/$PSC/confirm" '{}' "$ATOK")"
expect "onay sonrası Status=Completed" Completed "$(sql "SELECT \"Status\" FROM \"ClassSessions\" WHERE \"Id\"='$PSC';")"
expect "işaretlenmemiş öğrenci Present oldu" Present "$(sql "SELECT \"Status\" FROM \"Attendances\" WHERE \"ClassBookingId\"='$BC';")"
expect "idempotent: tekrar onay → 200 (no-op)" 200 "$(pcode "/api/studio/class-sessions/$PSC/confirm" '{}' "$ATOK")"

echo "--- 6) Bugünü onayla ---"
PST=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT14:00:00)"); book "$PST" "$S1" >/dev/null; backdate "$PST" "$TODAY 00:30:00"
RT=$(post "/api/studio/class-sessions/confirm-today" '{}' "$ATOK")
expect "confirm-today → 200" 200 "$(code "$RT")"
CC=$(body "$RT" | grep -o '"confirmedCount":[0-9]*' | sed 's/.*://')
expect "bugün ≥1 ders onaylandı" yes "$([ "${CC:-0}" -ge 1 ] && echo yes || echo no)"
expect "confirm-today idempotent (tekrar 0 onay)" 0 "$(body "$(post "/api/studio/class-sessions/confirm-today" '{}' "$ATOK")" | grep -o '"confirmedCount":[0-9]*' | sed 's/.*://')"

echo "--- 7) Fiilî hoca (substitution) ---"
PSA=$(mksess "$I1P" "$(date -d "+3 days" +%Y-%m-%dT15:00:00)"); BA=$(book "$PSA" "$S1"); backdate "$PSA" "2024-03-08 10:00:00"
mark "$PSA" "$BA" Present >/dev/null
expect "actual instructor set (I2P) → 200" 200 "$(pcode "/api/studio/class-sessions/$PSA/actual-instructor" "{\"actualInstructorProfileId\":\"$I2P\"}" "$ATOK")"
expect "session.ActualInstructorProfileId = I2P" "$I2P" "$(sql "SELECT \"ActualInstructorProfileId\" FROM \"ClassSessions\" WHERE \"Id\"='$PSA';")"
# Rapor 2024-03 ayında: ders FİİLÎ hocaya (I2P) atfedilir, planlanan I1P'ye değil.
REP=$(curl -s "$BASE/api/studio/reports/instructors?month=2024-03" -H "Authorization: Bearer $ATOK")
I2DS=$(echo "$REP" | tr '{' '\n' | grep "\"instructorProfileId\":\"$I2P\"" | grep -o '"distinctStudents":[0-9]*' | sed 's/.*://')
expect "rapor: PSA fiilî hoca I2P'ye yazıldı (distinctStudents≥1)" yes "$([ "${I2DS:-0}" -ge 1 ] && echo yes || echo no)"
expect "rapor PARASIZ (estimatedEarning alanı yok)" no "$(has "$REP" 'estimatedEarning')"
expect "rapor operasyonel alan içerir (groupSessions)" yes "$(has "$REP" 'groupSessions')"
# actual = planlanan'a eşitlersek temizlenir
pcode "/api/studio/class-sessions/$PSA/actual-instructor" "{\"actualInstructorProfileId\":\"$I1P\"}" "$ATOK" >/dev/null
expect "actual=planlanan → temizlendi (NULL → planlanan sayılır)" "" "$(sql "SELECT \"ActualInstructorProfileId\" FROM \"ClassSessions\" WHERE \"Id\"='$PSA';")"

echo "--- 8) Yetkiler ---"
expect "Instructor I2 başka hocanın dersini onayla → 403" 403 "$(pcode "/api/studio/class-sessions/$PSC/confirm" '{}' "$I2TOK")"
expect "Instructor I2 başka hocanın yoklaması → 403" 403 "$(putcode "/api/studio/class-sessions/$PSA/attendance" "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Absent\"}]}" "$I2TOK")"
expect "Instructor actual-instructor değiştiremez → 403" 403 "$(pcode "/api/studio/class-sessions/$PSA/actual-instructor" "{\"actualInstructorProfileId\":\"$I2P\"}" "$I2TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 19 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 19 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
