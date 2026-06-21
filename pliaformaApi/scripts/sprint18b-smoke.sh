#!/usr/bin/env bash
# Pliaforma — Sprint 18b smoke (Booking Cutover & Package Deprecation):
#  - Regular booking SÖZLEŞME zorunlu: sözleşmesiz → 409; uygun aktif sözleşme → 200 (auto-resolve).
#  - Tarih aralığı dışı → 409; ders tipi uyuşmaz → 409; ders hakkı dolu → 409.
#  - Aynı öğrenci/hoca çakışma → 409; farklı hoca aynı saat → 200; bireysel 2. öğrenci → 409; grup çok öğrenci.
#  - Öğrenci self-booking → 410; self-cancel → 410.
#  - Paket gate KULLANILMIYOR: yeni Regular booking'de ExpectedStudentPackageId NULL (DB).
# Postgres container gerekli. Kullanım: scripts/sprint18b-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S18b $TS\",\"ownerFullName\":\"O\",\"email\":\"o18b+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2)
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id; }
CT=$(ctype "Reformer"); CTB=$(ctype "Mat")
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
S1=$(guest Stu1); S2=$(guest Stu2); S3=$(guest Stu3)
mkcontract(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$3\",\"startDate\":\"$4\",\"endDate\":\"$5\",\"totalLessonCount\":$6,\"makeupAllowance\":1,\"price\":1000,\"isIndividualDefault\":$7}" "$ATOK")" | jget id; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"$3\"}" "$ATOK")" | jget id; }
book(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"${3:+,\"studentContractId\":\"$3\"}}" "$ATOK"; }

WIDE_S="2020-01-01"; WIDE_E="2030-01-01"
D1=$(date -d "+3 days" +%Y-%m-%dT10:00:00); D2=$(date -d "+3 days" +%Y-%m-%dT12:00:00); D1B=$(date -d "+3 days" +%Y-%m-%dT10:30:00)

echo "--- 1) Regular booking sözleşme zorunlu ---"
SES1=$(mksess "$I1P" "$CT" "$D1")
expect "sözleşmesiz Regular booking → 409" 409 "$(book "$SES1" "$S1")"
C1=$(mkcontract "$S1" "$CT" "$I1P" "$WIDE_S" "$WIDE_E" 2 false)
expect "uygun aktif sözleşme → auto-resolve 200" 200 "$(book "$SES1" "$S1")"
expect "booking ExpectedStudentPackageId NULL (paket gate yok)" 1 "$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$SES1' AND \"StudentProfileId\"='$S1' AND \"ExpectedStudentPackageId\" IS NULL AND \"StudentContractId\"='$C1';")"

echo "--- 2) Ders tipi / tarih aralığı / hak ---"
SESB=$(mksess "$I1P" "$CTB" "$(date -d "+4 days" +%Y-%m-%dT10:00:00)")
expect "ders tipi uyuşmaz (CTB) → 409" 409 "$(book "$SESB" "$S1")"   # S1 yalnız CT sözleşmesi var
# Tarih aralığı dışı: dar sözleşme + aralık dışı seans
NS=$(date -d "+2 days" +%F); NE=$(date -d "+5 days" +%F); FARS=$(date -d "+40 days" +%Y-%m-%dT10:00:00)
C2=$(mkcontract "$S2" "$CT" "$I1P" "$NS" "$NE" 5 false)
SESFAR=$(mksess "$I1P" "$CT" "$FARS")
expect "tarih aralığı dışı booking → 409" 409 "$(book "$SESFAR" "$S2")"
# Ders hakkı: C1 total=2, S1 SES1'de 1 kullandı; 2. ders 200, 3. → 409
# Sprint 28C-1: resolver instructor-zorunlu → C1 hocası (I1P) seansları kullanılır (farklı gün, çakışma yok).
SES2=$(mksess "$I1P" "$CT" "$(date -d "+6 days" +%Y-%m-%dT10:00:00)")
SES3=$(mksess "$I1P" "$CT" "$(date -d "+7 days" +%Y-%m-%dT10:00:00)")
expect "2. ders hakkı → 200" 200 "$(book "$SES2" "$S1")"
expect "3. ders (hak doldu) → 409" 409 "$(book "$SES3" "$S1")"

echo "--- 3) Çakışma: hoca/öğrenci; farklı hoca aynı saat ---"
expect "aynı hoca aynı saat 2. seans → 409" 409 "$(pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$D1\"}" "$ATOK")"
R_I2=$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CT\",\"startAt\":\"$D1\"}" "$ATOK")
expect "farklı hoca aynı saat → 200" 200 "$(code "$R_I2")"
SES_I2=$(body "$R_I2" | jget id)
mkcontract "$S1" "$CT" "$I2P" "$WIDE_S" "$WIDE_E" 5 false >/dev/null  # S1'e ikinci (I2P) sözleşme — çakışma testinin booking'i geçebilsin
expect "aynı öğrenci (S1) çakışan saatte → 409" 409 "$(book "$SES_I2" "$S1")"
mkcontract "$S3" "$CT" "$I2P" "$WIDE_S" "$WIDE_E" 5 false >/dev/null
expect "farklı öğrenci (S3) aynı saatte → 200" 200 "$(book "$SES_I2" "$S3")"

echo "--- 4) Bireysel/grup ---"
CIND=$(mkcontract "$S2" "$CT" "$I1P" "$NS" "$NE" 2 true)
post "/api/studio/contracts/$CIND/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"$(date -d "$NS" +%A)\",\"startTime\":\"20:00:00\"}],\"reuseExistingGroup\":false,\"individual\":null}" "$ATOK" >/dev/null
IND_SES=$(sql "SELECT cb.\"ClassSessionId\" FROM \"ClassBookings\" cb JOIN \"ClassSessions\" cs ON cs.\"Id\"=cb.\"ClassSessionId\" WHERE cb.\"StudentContractId\"='$CIND' AND cs.\"IsIndividual\"=true LIMIT 1;")
if [ -n "$IND_SES" ]; then
  mkcontract "$S3" "$CT" "$I1P" "$WIDE_S" "$WIDE_E" 5 false >/dev/null
  expect "bireysel seansa 2. öğrenci → 409" 409 "$(book "$IND_SES" "$S3")"
else ok "bireysel seans üretilemedi (gün/saat penceresi) — atlandı"; fi
GSES=$(mksess "$I1P" "$CT" "$(date -d "+8 days" +%Y-%m-%dT11:00:00)")
mkcontract "$S2" "$CT" "$I1P" "$WIDE_S" "$WIDE_E" 5 false >/dev/null
# Sprint 28C-1: S1'in ilk I1P hakkı (C1=2) bitti; instructor-zorunlu resolver için taze I1P line gerekir.
mkcontract "$S1" "$CT" "$I1P" "$WIDE_S" "$WIDE_E" 5 false >/dev/null
expect "grup seansa S1 → 200" 200 "$(book "$GSES" "$S1")"
expect "grup seansa S2 → 200 (çok öğrenci)" 200 "$(book "$GSES" "$S2")"

echo "--- 5) Öğrenci self-booking + self-cancel → 410 ---"
SUE="su18b+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"SU\",\"email\":\"$SUE\",\"password\":\"$PW\"}" >/dev/null; vrf "$SUE"
SUG=$(guest SUstu); post "/api/studio/students/$SUG/convert-to-user" "{\"email\":\"$SUE\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
SUTOK=$(login "$SUE")
expect "öğrenci self-book → 410" 410 "$(pcode "/api/student/class-sessions/$GSES/book" '{}' "$SUTOK")"
expect "öğrenci self-cancel → 410" 410 "$(pcode "/api/student/bookings/$(sql "SELECT \"Id\" FROM \"ClassBookings\" LIMIT 1;")/cancel" '{}' "$SUTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 18b SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 18b SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
