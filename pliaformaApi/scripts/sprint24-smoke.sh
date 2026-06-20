#!/usr/bin/env bash
# Pliaforma — Sprint 24 smoke (Student Self-Service Contract, Debt & Makeup Visibility — SALT-OKUNUR):
#  - Öğrenci kendi sözleşmesini + borcunu görür (/api/student/contracts, /api/student/debt; [Authorize Student]).
#  - Student A, Student B'nin sözleşme/borç bilgisini GÖREMEZ (resolve-own-profile; id parametresi yok).
#  - Instructor yeni student endpointlerinden veri ALAMAZ (403). Owner staff debt endpointi çalışır.
#  - createdByName / staff Notes / audit snapshot ÖĞRENCİYE SIZMAZ. Multi-currency: TEK toplam üretilmez.
#  - Öğrenci hiçbir yazma yapamaz (POST → 405). my-payments + my-makeup-credits bozulmaz.
# Postgres container gerekli. Kullanım: scripts/sprint24-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
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

OWNER="o24+$TS@pf.local"
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S24 $TS\",\"ownerFullName\":\"Owner24\",\"email\":\"$OWNER\",\"password\":\"$PW\"}")" | jget accessToken)
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2TOK=$(login "i1+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
SD=$(date +%F); ED=$(date -d "+74 days" +%F)
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$SD\",\"endDate\":\"$ED\",\"totalLessonCount\":8,\"makeupAllowance\":2,\"price\":$2,\"currency\":\"$3\",\"isIndividualDefault\":false}" "$ATOK")" | jget id; }
# Öğrenci kullanıcıya dönüştür → login token.
mkstudent(){ local sp="$1" e="$2"; post /api/auth/register "{\"fullName\":\"$3\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; post "/api/studio/students/$sp/convert-to-user" "{\"email\":\"$e\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; login "$e"; }

# Student A: TRY sözleşme (5000) + amendment (+4, +2000) → TRY 7000; ayrıca USD sözleşme (100) → multi-currency.
SA=$(guest "OgrenciA"); CA_TRY=$(mkc "$SA" 5000 TRY); CA_USD=$(mkc "$SA" 100 USD)
post "/api/studio/contracts/$CA_TRY/amendments" "{\"kind\":\"ExtraLessons\",\"additionalLessonCount\":4,\"additionalPrice\":2000,\"description\":\"Ek paket\"}" "$ATOK" >/dev/null
SATOK=$(mkstudent "$SA" "sa24+$TS@pf.local" "OgrenciA")
# Student B: TRY sözleşme (3000).
SB=$(guest "OgrenciB"); CB_TRY=$(mkc "$SB" 3000 TRY)
SBTOK=$(mkstudent "$SB" "sb24+$TS@pf.local" "OgrenciB")

echo "--- 1) Öğrenci kendi sözleşmesini görür ---"
ACON=$(get /api/student/contracts "$SATOK")
expect "A contracts → 200" 200 "$(gcode /api/student/contracts "$SATOK")"
expect "A kendi TRY sözleşmesini görür (Reformer)" yes "$(has "$ACON" "Reformer")"
expect "A sözleşmesinde toplam ders 12 (8+4 amendment)" yes "$(has "$ACON" '"totalLessonCount":12')"
expect "A amendment özeti görünür" yes "$(has "$ACON" '"additionalLessonCount":4')"

echo "--- 2) Internal alan SIZMAZ (createdByName / audit snapshot / staff price) ---"
expect "createdByName YOK" no "$(has "$ACON" "createdByName")"
expect "previousTotalLessonCount YOK" no "$(has "$ACON" "previousTotalLessonCount")"
expect "newTotalLessonCount YOK" no "$(has "$ACON" "newTotalLessonCount")"
expect "staff sözleşme Price alanı YOK (\"price\":)" no "$(echo "$ACON" | grep -qE '"price":' && echo yes || echo no)"

echo "--- 3) Öğrenci kendi borcunu görür (multi-currency AYRI) ---"
ADEBT=$(get /api/student/debt "$SATOK")
expect "A debt → 200" 200 "$(gcode /api/student/debt "$SATOK")"
expect "A debt TRY bloğu var" yes "$(has "$ADEBT" '"currency":"TRY"')"
expect "A debt USD bloğu var (ayrı)" yes "$(has "$ADEBT" '"currency":"USD"')"
# Sprint 28B: borç LINE-bazlı (remaining) — amendment debt'e KATILMAZ → TRY line 5000 (7000 değil).
expect "A TRY kalan 5000 (line; amendment hariç)" yes "$(has "$ADEBT" '"remaining":5000')"
expect "A USD kalan 100 (ayrı, birleştirilmedi)" yes "$(has "$ADEBT" '"remaining":100')"

echo "--- 4) İzolasyon: Student A, Student B verisini GÖREMEZ ---"
expect "A contracts B'nin sözleşmesini İÇERMEZ" no "$(has "$ACON" "$CB_TRY")"
expect "A debt B'nin sözleşmesini İÇERMEZ" no "$(has "$ADEBT" "$CB_TRY")"
BDEBT=$(get /api/student/debt "$SBTOK")
expect "B kendi borcunu görür (3000)" yes "$(has "$BDEBT" '"remaining":3000')"
expect "B debt A'nın TRY sözleşmesini İÇERMEZ" no "$(has "$BDEBT" "$CA_TRY")"
expect "B debt 5000 İÇERMEZ (A'nın TRY borcu)" no "$(has "$BDEBT" '"remaining":5000')"

echo "--- 5) Instructor yeni student endpointlerinden veri ALAMAZ (403) ---"
expect "Instructor /api/student/contracts → 403" 403 "$(gcode /api/student/contracts "$I2TOK")"
expect "Instructor /api/student/debt → 403" 403 "$(gcode /api/student/debt "$I2TOK")"

echo "--- 6) Owner staff debt endpointi çalışmaya devam eder ---"
expect "Owner /api/studio/students/{A}/debt → 200" 200 "$(gcode "/api/studio/students/$SA/debt" "$ATOK")"
expect "Owner staff debt A TRY kalan 5000 görür (line; amendment hariç)" yes "$(has "$(get "/api/studio/students/$SA/debt" "$ATOK")" '"remaining":5000')"

echo "--- 7) Salt-okunur: öğrenci yazma yapamaz ---"
expect "POST /api/student/contracts → 405" 405 "$(pcode /api/student/contracts '{}' "$SATOK")"
expect "POST /api/student/debt → 405" 405 "$(pcode /api/student/debt '{}' "$SATOK")"

echo "--- 8) Regresyon: my-payments + my-makeup-credits bozulmaz ---"
expect "öğrenci /api/student/payments → 200" 200 "$(gcode /api/student/payments "$SATOK")"
expect "öğrenci /api/student/makeup-credits → 200" 200 "$(gcode /api/student/makeup-credits "$SATOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 24 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 24 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
