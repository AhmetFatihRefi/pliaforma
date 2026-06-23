#!/usr/bin/env bash
# Pliaforma — Sprint 26 smoke (Remove Instructor Availability Constraint & Schedule-Based Instructor View):
#  - Sistem availability'ye göre ENGEL KOYMAZ: availability yokken/herhangi saatte ders → 200.
#  - HARD block'lar korunur: hoca çakışma 409, öğrenci çakışma 409, geçmiş 409.
#  - Ders atama staff-only (Sprint 26): Instructor session create → 403.
#  - Program üretimi / chain-shift / group-change / bulk-generate availability'ye TAKILMAZ.
#  - EK KARAR: ders tipi/grup/program tanımı staff-only (Instructor create/update/delete/bulk/group-change → 403);
#    Instructor kendi dersinde yoklama/onay akışı bozulmaz.
# Postgres container gerekli. Kullanım: scripts/sprint26-smoke.sh [BASE]
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
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
delcode(){ curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$2', \"EndAt\"='$2'::timestamp + interval '1 hour' WHERE \"Id\"='$1';" >/dev/null; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S26 $TS\",\"ownerFullName\":\"O\",\"email\":\"o26+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2); I1TOK=$(login "i1+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
mksess(){ post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\"}" "$ATOK"; }
S1=$(guest S1)
D3=$(date -d "+3 days" +%Y-%m-%d)
# S1'e her iki hocada sözleşme (Regular booking sözleşme ister; öğrenci-çakışması testi için ikisi de gerekir).
# Sprint 28C-1: booking+generate line hakkını tüketir → sonraki yoklama/onay booking'i için bol hak (30).
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$2\",\"startDate\":\"$(date +%F)\",\"endDate\":\"$(date -d '+30 days' +%F)\",\"totalLessonCount\":30,\"makeupAllowance\":0,\"price\":0,\"isIndividualDefault\":false}" "$ATOK")" | jget id; }
C1=$(mkc "$S1" "$I1P"); C2=$(mkc "$S1" "$I2P")

echo "--- 1) Availability'ye göre ENGEL YOK + HARD block'lar korunur ---"
expect "availability yokken ders (10:00) → 200" 200 "$(code "$(mksess "$I1P" "${D3}T10:00:00")")"
expect "herhangi saat (gece 22:00, 'pencere dışı') → 200" 200 "$(code "$(mksess "$I1P" "${D3}T22:00:00")")"
expect "hoca çakışma (10:30, 10:00 ile örtüşür) → 409" 409 "$(pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${D3}T10:30:00\"}" "$ATOK")"
expect "geçmiş zaman → 409" 409 "$(pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"2020-01-01T10:00:00\"}" "$ATOK")"
expect "Instructor session create → 403 (staff-only)" 403 "$(pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${D3}T11:00:00\"}" "$I1TOK")"

echo "--- 2) Öğrenci çakışması korunur ---"
SA=$(body "$(mksess "$I1P" "${D3}T14:00:00")" | jget id)
SB=$(body "$(mksess "$I2P" "${D3}T14:00:00")" | jget id)   # farklı hoca, aynı saat (hoca çakışması yok)
expect "S1 ilk derse booking → 200" 200 "$(pcode "/api/studio/class-sessions/$SA/bookings" "{\"studentProfileId\":\"$S1\"}" "$ATOK")"
expect "S1 aynı saatte 2. derse booking → 409 (öğrenci çakışması)" 409 "$(pcode "/api/studio/class-sessions/$SB/bookings" "{\"studentProfileId\":\"$S1\"}" "$ATOK")"

echo "--- 3) Program üretimi availability'siz çalışır ---"
GEN=$(post "/api/studio/contracts/$C1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"19:00:00\"},{\"dayOfWeek\":\"Thursday\",\"startTime\":\"19:00:00\"}],\"reuseExistingGroup\":true,\"individual\":null}" "$ATOK")
expect "contract generate → 200 (availability gerekmez)" 200 "$(code "$GEN")"
expect "program üretildi (≥1 ders)" yes "$(echo "$(body "$GEN")" | grep -o '"createdCount":[1-9]' >/dev/null && echo yes || echo no)"

echo "--- 4) Chain-shift availability'ye takılmaz ---"
CS=$(body "$(mksess "$I1P" "${D3}T15:00:00")" | jget id)
expect "chain-shift (15:00 → 17:00, gece) → 200" 200 "$(pcode "/api/studio/class-sessions/$CS/chain-shift" "{\"newStartAt\":\"${D3}T23:00:00\"}" "$ATOK")"

echo "--- 5) Bulk-generate 'outside-availability' ATLAMAZ ---"
D4=$(date -d "+4 days" +%Y-%m-%d)
BG=$(post /api/studio/class-sessions/bulk-generate "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CT\",\"date\":\"$D4\",\"rangeStart\":\"09:00:00\",\"rangeEnd\":\"12:00:00\"}" "$ATOK")
expect "bulk-generate → 200" 200 "$(code "$BG")"
expect "09:00 üretildi (availability gerekmez)" yes "$(has "$(body "$BG")" "${D4}T09:00:00")"
expect "skipped'ta 'outside-availability' YOK" no "$(has "$(body "$BG")" 'outside-availability')"

echo "--- 6) Group-change candidates availability'siz çalışır ---"
expect "group-change candidates → 200" 200 "$(gcode "/api/studio/group-schedule-change/candidates?instructorProfileId=$I1P&classTypeId=$CT&effectiveDate=$(date +%F)" "$ATOK")"

echo "--- 7) Ek karar: ders tipi/grup/program tanımı staff-only (Instructor → 403) ---"
CTBODY='{"name":"X","equipment":"Reformer","capacityType":"Group","maxCapacity":6,"level":"AllLevels","purpose":"General","defaultDurationMinutes":60}'
expect "Instructor ders tipi create → 403" 403 "$(pcode /api/class-types "$CTBODY" "$I1TOK")"
expect "Instructor ders tipi update → 403" 403 "$(putcode "/api/class-types/$CT" "$CTBODY" "$I1TOK")"
expect "Instructor ders tipi delete → 403" 403 "$(delcode "/api/class-types/$CT" "$I1TOK")"
expect "Instructor bulk-generate → 403" 403 "$(pcode /api/studio/class-sessions/bulk-generate "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"date\":\"$D3\",\"rangeStart\":\"09:00:00\",\"rangeEnd\":\"12:00:00\"}" "$I1TOK")"
expect "Instructor group-change preview → 403" 403 "$(pcode /api/studio/group-schedule-change/preview "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"effectiveDate\":\"$(date +%F)\",\"contractIds\":[],\"newSlots\":[]}" "$I1TOK")"
expect "Instructor telafi kredisi GRANT → 403 (kredi veremez)" 403 "$(pcode "/api/studio/students/$S1/makeup-credits/grant" '{"amount":1,"notes":"x"}' "$I1TOK")"
expect "Instructor telafi kredisi REVOKE → 403" 403 "$(pcode "/api/studio/students/$S1/makeup-credits/revoke" '{"amount":1,"notes":"x"}' "$I1TOK")"
expect "Instructor misafir öğrenci oluştur → 403 (öğrenci yönetimi)" 403 "$(pcode /api/studio/students/guest '{"fullName":"Y","source":"Phone"}' "$I1TOK")"
expect "Instructor öğrenci pasifleştir → 403" 403 "$(pcode "/api/studio/students/$S1/deactivate" '{}' "$I1TOK")"
echo "--- 7b) v1.1.2: Instructor global öğrenci listesi/okuma + booking ekleme erişemez ---"
expect "Instructor öğrenci listesi (GET) → 403 (PII; akış muhtaç değil)" 403 "$(gcode /api/studio/students "$I1TOK")"
expect "Instructor öğrenci arama (GET) → 403" 403 "$(gcode "/api/studio/students/search?query=x" "$I1TOK")"
expect "Instructor öğrenci by-id (GET) → 403" 403 "$(gcode "/api/studio/students/$S1" "$I1TOK")"
SADD=$(body "$(mksess "$I1P" "${D3}T07:00:00")" | jget id)
expect "Instructor seansa öğrenci EKLE (booking) → 403" 403 "$(pcode "/api/studio/class-sessions/$SADD/bookings" "{\"studentProfileId\":\"$S1\"}" "$I1TOK")"
expect "Instructor booking iptal → 403" 403 "$(pcode "/api/studio/bookings/00000000-0000-0000-0000-000000000000/cancel" '{}' "$I1TOK")"
# Owner bu işlemleri yapmaya devam eder.
expect "Owner ders tipi create → 200 (regresyon)" 200 "$(pcode /api/class-types "$CTBODY" "$ATOK")"
S1LINE=$(sql "SELECT l.\"Id\" FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE c.\"StudentProfileId\"='$S1' LIMIT 1;")
expect "Owner telafi kredisi GRANT (line) → 200 (regresyon)" 200 "$(pcode "/api/studio/students/$S1/makeup-credits/grant" "{\"studentContractLineId\":\"$S1LINE\",\"amount\":1,\"notes\":\"x\"}" "$ATOK")"
expect "Owner öğrenci listesi (GET) → 200 (regresyon)" 200 "$(gcode /api/studio/students "$ATOK")"

echo "--- 8) Ek karar: Instructor KENDİ dersinde yoklama/onay akışı BOZULMAZ ---"
CONF=$(body "$(mksess "$I1P" "${D3}T08:00:00")" | jget id); BC=$(body "$(post "/api/studio/class-sessions/$CONF/bookings" "{\"studentProfileId\":\"$S1\"}" "$ATOK")" | jget id); backdate "$CONF" "2024-03-04 08:00:00"
expect "Instructor kendi seansının bookings (GET) → 200 (yoklama bağlamı)" 200 "$(gcode "/api/studio/class-sessions/$CONF/bookings" "$I1TOK")"
expect "Instructor kendi seansında yoklama (Present) → 200" 200 "$(putcode "/api/studio/bookings/$BC/attendance" '{"status":"Present"}' "$I1TOK")"
expect "Instructor kendi (başlamış) dersini onayla → 200" 200 "$(pcode "/api/studio/class-sessions/$CONF/confirm" '{}' "$I1TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 26 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 26 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
