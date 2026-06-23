#!/usr/bin/env bash
# Pliaforma — Sprint 28A smoke (Contract Lines + ClassType sadeleştirme + Student.Level):
#  - ClassType create yalnız ad+süre+açıklama ister (kaldırılan alanlara muhtaç değil); response sade.
#  - CreateClassSession kapasitesiz → grup-sınırsız (MaxCapacity'ye bağlı değil).
#  - StudentProfile.Level set/read.
#  - Her contract → 1 line (read-only); backfill ile Payment/Booking line'a bağlanır (line.paid/scheduled yansır);
#    makeup ledger booking üzerinden line'a bağlanır; bağlanamayan (manuel grant) → soft-delete.
# Postgres container + node gerekli. Kullanım: scripts/sprint28a-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
cnt(){ echo "$1" | grep -o "$2" | wc -l | tr -d ' '; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
put(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$2', \"EndAt\"='$2'::timestamp + interval '1 hour' WHERE \"Id\"='$1';" >/dev/null; }
bal(){ gbody "/api/studio/students/$1/makeup-credits" "$ATOK" | grep -o '"balance":[0-9-]*' | head -1; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S28A $TS\",\"ownerFullName\":\"O\",\"email\":\"o28a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
e="i1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"I1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; I1P=$(ipid "$e")
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"${2:+,$2}}" "$ATOK")"; }
D5=$(date -d "+5 days" +%Y-%m-%d); TODAY=$(date +%F); END=$(date -d "+30 days" +%F)

echo "--- 1) ClassType sadeleşti (yalnız ad+süre+açıklama; response sade) ---"
RCT=$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60,\"description\":\"d\"}" "$ATOK")
expect "ClassType create (sade gövde) → 200" 200 "$(code "$RCT")"; CT=$(body "$RCT" | jget id)
expect "response süre içerir" yes "$(has "$(body "$RCT")" '"defaultDurationMinutes":60')"
expect "response maxCapacity İÇERMEZ" no "$(has "$(body "$RCT")" 'maxCapacity')"
expect "response equipment/level/purpose İÇERMEZ" no "$(has "$(body "$RCT")" 'equipment')"
# Eski gövde (fazladan alanlar) hâlâ kabul (seed/regresyon uyumu — fazlalık yok sayılır).
expect "ClassType create (eski fazla alanlı gövde) → 200 (regresyon)" 200 "$(pcode /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")"

echo "--- 2) CreateClassSession kapasitesiz → grup-sınırsız (MaxCapacity'ye bağlı değil) ---"
RSE=$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${D5}T10:00:00\"}" "$ATOK")
expect "Kapasitesiz seans create → 200" 200 "$(code "$RSE")"
expect "seans kapasite grup-sınırsız (9999)" yes "$(has "$(body "$RSE")" '"capacity":9999')"

echo "--- 3) Student.Level set/read ---"
RSL=$(guest "SL" "\"level\":\"Beginner\""); expect "Level ile öğrenci create → level=Beginner" yes "$(has "$RSL" '"level":"Beginner"')"
SL=$(echo "$RSL" | jget id)
RUP=$(put "/api/studio/students/$SL" "{\"fullName\":\"SL\",\"source\":\"Phone\",\"level\":\"Advanced\"}" "$ATOK")
expect "Level update → Advanced" yes "$(has "$(body "$RUP")" '"level":"Advanced"')"
expect "Levelsiz öğrenci create → level YOK (null omit)" no "$(has "$(guest "NL")" '"level":"')"

echo "--- 4) Her contract → 1 line (read-only) + alanlar doğru ---"
SX=$(echo "$(guest "SX")" | jget id)
CX=$(body "$(post "/api/studio/students/$SX/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"totalLessonCount\":4,\"makeupAllowance\":2,\"price\":400,\"isIndividualDefault\":false}" "$ATOK")" | jget id)
LINES=$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK")
expect "contract sonrası tam 1 line" 1 "$(cnt "$LINES" '"id":"')"
expect "line lessonCount=4" yes "$(has "$LINES" '"lessonCount":4')"
expect "line totalPrice=400" yes "$(has "$LINES" '"totalPrice":400')"
expect "line pricePerLesson=100" yes "$(has "$LINES" '"pricePerLesson":100')"
expect "line dueDate YOK (backfill/oluşturmada NULL → omit)" no "$(has "$LINES" '"dueDate":"')"
expect "line makeupAllowance=2" yes "$(has "$LINES" '"makeupAllowance":2')"

echo "--- 5) Payment + Booking + Makeup → backfill → line'a bağlanma ---"
# Ödeme (sözleşmeli, 150). Sprint 29A strict: API artık line-zorunlu → legacy null-line+contractId Paid ödeme SQL ile
# (pre-29A veri simülasyonu); backfill bunu line'a bağlar (aşağıda line.paid=150 doğrulanır).
sql "INSERT INTO \"Payments\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"StudentContractId\",\"Amount\",\"Currency\",\"DueDate\",\"Status\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\") SELECT gen_random_uuid(), \"StudioId\", \"Id\", '$CX', 150, 'TRY', CURRENT_DATE, 'Paid', '00000000-0000-0000-0000-000000000000', now(), false FROM \"StudentProfiles\" WHERE \"Id\"='$SX';" >/dev/null
# Booking: seans + öğrenciyi rezerve et (Regular → sözleşme auto-resolve).
SB=$(body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${D5}T12:00:00\"}" "$ATOK")" | jget id)
BK=$(body "$(post "/api/studio/class-sessions/$SB/bookings" "{\"studentProfileId\":\"$SX\"}" "$ATOK")" | jget id)
expect "Regular booking (sözleşme auto-resolve) → oluştu" yes "$([ -n "$BK" ] && echo yes || echo no)"
# Devamsızlık → AbsentEarned kredi (SourceBooking=BK). Seansı geçmişe çek, Absent işaretle.
backdate "$SB" "2024-03-04 12:00:00"
expect "Absent yoklama → 200" 200 "$(putcode "/api/studio/bookings/$BK/attendance" '{"status":"Absent"}' "$ATOK")"
# Manuel grant (kaynak booking YOK → backfill'de bağlanamaz → soft-delete).
# Sprint 28C-2: API grant artık lineId zorunlu; bu backfill senaryosu LEGACY lineId=NULL kredi gerektirir → SQL ile ekle.
sql "INSERT INTO \"MakeupCreditTransactions\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"Amount\",\"Type\",\"Reason\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\") SELECT gen_random_uuid(), \"StudioId\", \"Id\", 1, 'Granted', 'ManualByOwner', '00000000-0000-0000-0000-000000000000', now(), false FROM \"StudentProfiles\" WHERE \"Id\"='$SX';" >/dev/null
expect "backfill öncesi bakiye = 2 (absence + manual)" yes "$(has "$(bal "$SX")" '"balance":2')"

# Backfill (idempotent, global).
node "$(dirname "$0")/sprint28a-backfill.mjs" >/tmp/pf-28a-backfill.log 2>&1 && ok "backfill çalıştı" || bad "backfill FAIL ($(tail -2 /tmp/pf-28a-backfill.log))"

LINES2=$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK")
expect "backfill sonrası payment line'a bağlı → line.paid=150" yes "$(has "$LINES2" '"paid":150')"
expect "line.remaining=250" yes "$(has "$LINES2" '"remaining":250')"
expect "backfill sonrası booking line'a bağlı → scheduledLessonCount=1" yes "$(has "$LINES2" '"scheduledLessonCount":1')"
expect "line.availableLessonCount=3" yes "$(has "$LINES2" '"availableLessonCount":3')"
expect "backfill sonrası bakiye = 1 (absence line'a bağlandı, manual soft-delete)" yes "$(has "$(bal "$SX")" '"balance":1')"

echo
echo "Backfill raporu:"; sed -n 's/^/    /p' /tmp/pf-28a-backfill.log | grep -E "Sözleşme|Payment|ClassBooking|Makeup|MakeupRequest|line" | head
echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 28A SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 28A SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
