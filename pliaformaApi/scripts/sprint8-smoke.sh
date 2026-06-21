#!/usr/bin/env bash
# Pliaforma — Sprint 8 smoke (dashboard + raporlar: studio/instructor/student, earning, tenant, rol).
# Kullanım: scripts/sprint8-smoke.sh [BASE]  (vars. http://localhost:5132)
# Not: attendance "başlamış seans" ister → seans ileri açılıp psql ile 2020-01'e çekilir; dashboard'lar
# month=2020-01 ile bu veriyi okur. Postgres container otomatik tespit edilir (native/compose).
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":-\?[0-9]*" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
pucode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (backdate gerekli)."; exit 2; }
backdate(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"ClassSessions\" SET \"StartAt\"='2020-01-15 10:00:00', \"EndAt\"='2020-01-15 11:00:00' WHERE \"Id\"='$1';" >/dev/null 2>&1; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }

# --- Stüdyolar + hocalar + grup ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S8A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s8a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S8C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s8c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
for n in i1 i2; do post /api/auth/register "{\"fullName\":\"$n\",\"email\":\"$n+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null; done
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
inv(){ local e="$1+$TS@pf.local"; vrf "$e"; RR=$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); M=$(body "$RR" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
I1P=$(inv i1); I2P=$(inv i2); I1TOK=$(login "i1+$TS@pf.local"); I2TOK=$(login "i2+$TS@pf.local")
# Ücret tipleri: i1 PerSession 100, i2 PerStudentPerSession 50
pucode "/api/instructors/$I1P/rate" "{\"rate\":100,\"rateType\":\"PerSession\"}" "$ATOK" >/dev/null
pucode "/api/instructors/$I2P/rate" "{\"rate\":50,\"rateType\":\"PerStudentPerSession\"}" "$ATOK" >/dev/null
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
D=$(date -d "+3 days" +%Y-%m-%d)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"${D}T$2:00\",\"capacity\":5}" "$ATOK")" | jget id; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
sbook(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }
markp(){ putcode "/api/studio/class-sessions/$1/attendance" "{\"items\":[{\"bookingId\":\"$2\",\"status\":\"Present\"}]}" "$ATOK"; }

# --- Veri: i1 2 seans (PerSession), i2 1 seans (PerStudent) — hepsi 2020-01'e çekilir, Present ---
GA=$(guest "GA"); GB=$(guest "GB"); GC=$(guest "GC"); GD=$(guest "GD")
# Sprint 18b: Regular booking sözleşme ister → booked öğrencilere geniş sözleşme (CT; auto-resolve).
# Sprint 28C-1: booking resolver instructor-zorunlu → öğrencinin sözleşmesi seansın hocasıyla eşleşmeli ($2=hoca, vars. I1P).
mkc(){ curl -s -o /dev/null -X POST "$BASE/api/studio/students/$1/contracts" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"${2:-$I1P}\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":100,\"makeupAllowance\":5,\"price\":0,\"isIndividualDefault\":false}"; }
mkc "$GA"; mkc "$GB"; mkc "$GC" "$I2P"; mkc "$GD" "$I2P"   # GC,GD I2P seansında → I2P sözleşme
S1=$(mksess "$I1P" "10:00"); BA=$(sbook "$S1" "$GA"); backdate "$S1"; markp "$S1" "$BA" >/dev/null
S2=$(mksess "$I1P" "11:00"); BB=$(sbook "$S2" "$GB"); backdate "$S2"; markp "$S2" "$BB" >/dev/null
S3=$(mksess "$I2P" "12:00"); BC=$(sbook "$S3" "$GC"); BD=$(sbook "$S3" "$GD"); backdate "$S3"
putcode "/api/studio/class-sessions/$S3/attendance" "{\"items\":[{\"bookingId\":\"$BC\",\"status\":\"Present\"},{\"bookingId\":\"$BD\",\"status\":\"Present\"}]}" "$ATOK" >/dev/null
# Paket + ödeme + makeup (GA)
PL=$(body "$(post /api/studio/package-plans "{\"name\":\"4 Ders $TS\",\"creditCount\":4,\"price\":1000}" "$ATOK")" | jget id)
post "/api/studio/students/$GA/packages" "{\"packagePlanId\":\"$PL\"}" "$ATOK" >/dev/null
# Sprint 29A: strict payment → ödeme line'a bağlanır (GA/GB sözleşmeli → line var).
GAL=$(get "/api/studio/students/$GA/contract-lines" "$ATOK" | jget id)
GBL=$(get "/api/studio/students/$GB/contract-lines" "$ATOK" | jget id)
post "/api/studio/students/$GA/payments" "{\"amount\":500,\"dueDate\":\"2020-01-20\",\"studentContractLineId\":\"$GAL\"}" "$ATOK" >/dev/null   # geçmiş vade → Overdue
PJ=$(post "/api/studio/students/$GB/payments" "{\"amount\":300,\"dueDate\":\"2020-01-20\",\"studentContractLineId\":\"$GBL\"}" "$ATOK"); PID=$(body "$PJ" | jget id)
pcode "/api/studio/payments/$PID/mark-paid" '{}' "$ATOK" >/dev/null
# Sprint 28C-2: manuel grant lineId zorunlu → GA'nın line'ına yaz.
GALINE=$(sql "SELECT l.\"Id\" FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE c.\"StudentProfileId\"='$GA' LIMIT 1;")
post "/api/studio/students/$GA/makeup-credits/grant" "{\"studentContractLineId\":\"$GALINE\",\"amount\":1}" "$ATOK" >/dev/null

echo "--- Studio dashboard (month=2020-01) ---"
SD=$(get "/api/studio/dashboard?month=2020-01" "$ATOK")
expect "sessionsThisMonth=3" 3 "$(echo "$SD" | jnum sessionsThisMonth)"
expect "bookingsThisMonth=4" 4 "$(echo "$SD" | jnum bookingsThisMonth)"
expect "attendance presentCount=4" 4 "$(echo "$SD" | jnum presentCount)"
expect "payments overdueAmount=500 (dinamik)" 500 "$(echo "$SD" | jnum overdueAmount)"
expect "payments paidAmount=300" 300 "$(echo "$SD" | jnum paidAmount)"
expect "packages remainingCreditTotal=4" 4 "$(echo "$SD" | jnum remainingCreditTotal)"
expect "makeup totalOutstandingBalance=1" 1 "$(echo "$SD" | jnum totalOutstandingBalance)"

echo "--- Month filter ayırıyor ---"
expect "boş ay (2025-01) sessionsThisMonth=0" 0 "$(get "/api/studio/dashboard?month=2025-01" "$ATOK" | jnum sessionsThisMonth)"

echo "--- Instructor dashboard (operasyonel, PARASIZ — Sprint 19) ---"
I1D=$(get "/api/instructor/dashboard?month=2020-01" "$I1TOK")
expect "i1 sessionsThisMonth=2" 2 "$(echo "$I1D" | jnum sessionsThisMonth)"
expect "i1 totalStudentsTaught=2" 2 "$(echo "$I1D" | jnum totalStudentsTaught)"
expect "dashboard PARASIZ (estimatedEarning alanı yok)" no "$(has "$I1D" 'estimatedEarning')"
expect "i1 self dashboard → 200" 200 "$(gcode "/api/instructor/dashboard?month=2020-01" "$I1TOK")"

echo "--- Student dashboard (GA) ---"
post "/api/studio/students/$GA/convert-to-user" "{\"email\":\"ga+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
GATOK=$(login "ga+$TS@pf.local")
GAD=$(get "/api/student/dashboard?month=2020-01" "$GATOK")
expect "GA self dashboard → 200" 200 "$(gcode "/api/student/dashboard?month=2020-01" "$GATOK")"
expect "GA kalan paket hakkı 4" 4 "$(echo "$GAD" | jnum totalRemainingCredits)"
expect "GA makeup balance 1" 1 "$(echo "$GAD" | jnum makeupCreditBalance)"
expect "GA overdue ödeme 500" 500 "$(echo "$GAD" | jnum overdueAmount)"
expect "GA attendance presentCount 1" 1 "$(echo "$GAD" | jnum presentCount)"

echo "--- Reports ---"
expect "payments report → 200" 200 "$(gcode "/api/studio/reports/payments?month=2020-01" "$ATOK")"
expect "payments report overdueAmount=500" 500 "$(get "/api/studio/reports/payments?month=2020-01" "$ATOK" | jnum overdueAmount)"
IR=$(get "/api/studio/reports/instructors?month=2020-01" "$ATOK")
expect "instructors report PARASIZ (estimatedEarning yok)" no "$(has "$IR" 'estimatedEarning')"
expect "instructors report operasyonel (distinctStudents var)" yes "$(has "$IR" 'distinctStudents')"

echo "--- Yetki: Instructor staff student dashboard (finans gizli) ---"
expect "I1 erişebildiği öğrenci dashboard → 200" 200 "$(gcode "/api/studio/students/$GA/dashboard?month=2020-01" "$I1TOK")"
expect "I1 için paymentSummary YOK (finans gizli, null omitted)" no "$(has "$(get "/api/studio/students/$GA/dashboard?month=2020-01" "$I1TOK")" '"paymentSummary"')"
expect "Owner için paymentSummary VAR" yes "$(has "$(get "/api/studio/students/$GA/dashboard?month=2020-01" "$ATOK")" '"paymentSummary"')"
expect "I2 erişemediği öğrenci dashboard → 403" 403 "$(gcode "/api/studio/students/$GA/dashboard?month=2020-01" "$I2TOK")"

echo "--- Yetki: rol kapıları ---"
expect "I2 başka hoca dashboard (by-id) → 403" 403 "$(gcode "/api/studio/instructors/$I1P/dashboard" "$I2TOK")"
expect "Student studio dashboard → 403" 403 "$(gcode /api/studio/dashboard "$GATOK")"
expect "Student instructor dashboard → 403" 403 "$(gcode /api/instructor/dashboard "$GATOK")"
expect "Student staff student dashboard → 403" 403 "$(gcode "/api/studio/students/$GA/dashboard" "$GATOK")"

echo "--- Tenant izolasyonu (C) ---"
expect "C boş dashboard → 200 (patlama yok)" 200 "$(gcode /api/studio/dashboard "$CTOK")"
expect "C, A hocasının dashboard'u → 404" 404 "$(gcode "/api/studio/instructors/$I1P/dashboard" "$CTOK")"
expect "C, A öğrencisinin dashboard'u → 404" 404 "$(gcode "/api/studio/students/$GA/dashboard" "$CTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 8 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 8 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
