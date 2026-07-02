#!/usr/bin/env bash
# Pliaforma — Sprint 45 smoke (Taksitli odeme plani kaldirildi -> tek "Son odeme tarihi"):
#  A. Yeni line create: dueDate zorunlu (yoksa 400); doluysa 200.
#  B. Payment create: dogrudan Paid + PaidAt set (Pending/vade UI'dan yok).
#  C. Line borc/gecikme (tek kaynak, ContractLineFeature): remaining floor, overdueAmount, daysOverdue,
#     today==dueDate gecikmis DEGIL, dueDate<today+kalan>0 -> overdue, kismi odeme azaltir + dueDate degismez,
#     tam odeme -> overdue 0, overpay -> remaining 0 + overpaidAmount>0.
#  D. Legacy: dueDate null -> isOverdue false ("belirlenmemis").
#  E. Payment-plan endpoint soft-removed -> 410 Gone.
# ASCII-only. Postgres opsiyonel (yalnizca varsa email verify icin kullanilir).
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$2" | grep -q "$3" && ok "$1" || bad "$1 (icermiyor: $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ echo "$1" | grep -o "\"$2\":-\\?[0-9.]*" | head -1 | sed "s/\"$2\"://"; }
jbool(){ echo "$1" | grep -o "\"$2\":\(true\|false\)" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
vrf(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
TS=$(date +%s)
TODAY=$(date +%F); YDAY=$(date -d "-1 day" +%F); F20=$(date -d "+20 days" +%F); END=$(date -d "+200 days" +%F)
ipid(){ gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 45: tek son odeme tarihi + alinan odeme (taksitli plan kaldirildi) =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S45 $TS\",\"ownerFullName\":\"O\",\"email\":\"o45+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 2; }
e="hocaA45+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"hocaA\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null
IA=$(ipid "$e")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
SLOTS='[{"dayOfWeek":"Monday","preferredStartTime":null,"notes":null}]'
# addlineRaw $student $lessonCount $price $dueDateJson(ör: "\"2026-01-01\"" veya "null") -> raw response (code+body)
addlineRaw(){ post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$IA\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$2,\"pricePerLesson\":$3,\"makeupAllowance\":1,\"dueDate\":$4,\"currency\":\"TRY\",\"preferredSlots\":$SLOTS}" "$ATOK"; }
addline(){ body "$(addlineRaw "$1" "$2" "$3" "\"$4\"")" | jget id; }
# pay $student $lineId $amount -> alinan odeme id (dogrudan Paid)
payRaw(){ post "/api/studio/students/$1/payments" "{\"amount\":$3,\"studentContractLineId\":\"$2\"}" "$ATOK"; }
pay(){ body "$(payRaw "$1" "$2" "$3")" | jget id; }
lineOf(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | grep -o "{[^}]*\"id\":\"$2\"[^}]*}\|{\"id\":\"$2\"[^{]*"; }
# getLine $studentId $lineId -> tek line objesinin ham JSON parcasi (basit grep; alanlari duz JSON'da arar)
getLineField(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK"; }

echo "--- A) Yeni line create: Son odeme tarihi zorunlu ---"
SA=$(guest A45)
expect "A: dueDate null -> 400" 400 "$(code "$(addlineRaw "$SA" 6 1000 null)")"
LA=$(addline "$SA" 6 1000 "$F20")   # total 6000, vade +20 gun
expect "A: dueDate dolu -> line olustu" 1 "$([ -n "$LA" ] && echo 1 || echo 0)"

echo "--- B) Payment create: dogrudan Paid + PaidAt ---"
PB=$(body "$(payRaw "$SA" "$LA" 2000)")
has "B: create -> status Paid" "$PB" '"status":"Paid"'
has "B: create -> paidAt dolu (null degil)" "$PB" '"paidAt":"'

echo "--- C) Line borc/gecikme (kismi odeme, overdue siniri, tam odeme, overpay) ---"
SC=$(guest C45); LC=$(addline "$SC" 6 1000 "$F20")   # total 6000, vade GELECEK (henuz gecikmemis)
GC0=$(getLineField "$SC")
expectn "C: kismi odeme oncesi remaining=6000" 6000 "$(jnum "$GC0" remaining)"
expect  "C: vade gelecek -> isOverdue false" false "$(jbool "$GC0" isOverdue)"
pay "$SC" "$LC" 2000 >/dev/null
GC1=$(getLineField "$SC")
expectn "C: 2000 odendi -> remaining=4000" 4000 "$(jnum "$GC1" remaining)"
expectn "C: overdueAmount 0 (vade gelecek)" 0 "$(jnum "$GC1" overdueAmount)"

SD=$(guest D45); LD=$(addline "$SD" 1 4000 "$TODAY")   # vade BUGUN, hic odeme yok
GD=$(getLineField "$SD")
expect  "C: today==dueDate -> isOverdue false" false "$(jbool "$GD" isOverdue)"
expectn "C: today==dueDate -> overdueAmount 0" 0 "$(jnum "$GD" overdueAmount)"

SE=$(guest E45); LE=$(addline "$SE" 1 4000 "$YDAY")   # vade DUN, hic odeme yok
GE=$(getLineField "$SE")
expect  "C: dueDate<today + kalan>0 -> isOverdue true" true "$(jbool "$GE" isOverdue)"
expectn "C: overdueAmount = remaining (4000)" 4000 "$(jnum "$GE" overdueAmount)"
pay "$SE" "$LE" 1500 >/dev/null   # kismi odeme
GE1=$(getLineField "$SE")
expectn "C: kismi odeme sonrasi remaining 2500" 2500 "$(jnum "$GE1" remaining)"
expectn "C: kismi odeme gecikmis borcu azaltir (overdueAmount 2500)" 2500 "$(jnum "$GE1" overdueAmount)"
has     "C: dueDate DEGISMEDI (hala $YDAY)" "$GE1" "\"dueDate\":\"$YDAY\""
pay "$SE" "$LE" 2500 >/dev/null   # tam odeme
GE2=$(getLineField "$SE")
expectn "C: tam odeme sonrasi overdueAmount 0" 0 "$(jnum "$GE2" overdueAmount)"
expect  "C: tam odeme sonrasi isOverdue false" false "$(jbool "$GE2" isOverdue)"
pay "$SE" "$LE" 1000 >/dev/null   # fazla odeme (overpay)
GE3=$(getLineField "$SE")
expectn "C: overpay -> remaining 0 (floor)" 0 "$(jnum "$GE3" remaining)"
expectn "C: overpay -> overpaidAmount 1000" 1000 "$(jnum "$GE3" overpaidAmount)"

echo "--- D) Legacy: dueDate null -> gecikmis sayilmaz ---"
SF=$(guest F45)
# dueDate=null gecmeden once eklenen legacy davranisini simule etmek icin update ile null'a cekiyoruz (validator yalniz CREATE'te zorunlu).
LF=$(addline "$SF" 1 1000 "$YDAY")
putc "/api/studio/contract-lines/$LF" "{\"dueDate\":null,\"notes\":null}" "$ATOK" >/dev/null
GF=$(getLineField "$SF")
# Not: System.Text.Json null alanlari JSON'dan tamamen dusurur (WhenWritingNull) -> "dueDate" key'i hic gorunmez.
expect "D: dueDate null -> JSON'da dueDate alani yok" 0 "$(echo "$GF" | grep -c '"dueDate"')"
expect "D: dueDate null -> isOverdue false" false "$(jbool "$GF" isOverdue)"

echo "--- E) Payment-plan endpoint soft-removed (410 Gone) ---"
expect "E: GET payment-plan -> 410" 410 "$(gcode "/api/studio/contract-lines/$LA/payment-plan" "$ATOK")"
expect "E: PUT payment-plan -> 410" 410 "$(putc "/api/studio/contract-lines/$LA/payment-plan" '{"installments":[]}' "$ATOK")"

echo "--- F) Payment.DueDate kullanicidan gelmiyor + MarkPaid gerekmeden line.paid artiyor ---"
SG=$(guest G45); LG=$(addline "$SG" 4 500 "$F20")   # total 2000
PAID0=$(jnum "$(getLineField "$SG")" paid)
PGRESP=$(body "$(payRaw "$SG" "$LG" 800)")   # dueDate ALANI HIC GONDERILMEDI
has     "F: create body'de dueDate gonderilmedi ama response'ta today ile dolduruldu" "$PGRESP" "\"dueDate\":\"$TODAY\""
has     "F: response status Paid (vade sorulmadan)" "$PGRESP" '"status":"Paid"'
GG1=$(getLineField "$SG")
expectn "F: mark-paid CAGRILMADAN line.paid arttı (0 -> 800)" 800 "$(jnum "$GG1" paid)"
expectn "F: remaining otomatik dustu (2000 -> 1200)" 1200 "$(jnum "$GG1" remaining)"

echo "--- G/H) Dashboard + Reports: LINE-bazli kalan/gecikmis (Payment.Status/DueDate DEGIL) ---"
MONTHNOW=$(date +%Y-%m)
e2="hocaB45+$TS@pf.local"; BTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S45B $TS\",\"ownerFullName\":\"OB\",\"email\":\"ob45+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
post /api/auth/register "{\"fullName\":\"hocaB\",\"email\":\"$e2\",\"password\":\"$PW\"}" >/dev/null; vrf "$e2"
MB=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e2\",\"role\":\"Instructor\"}" "$BTOK")" | jget membershipId); TB=$(login "$e2"); post "/api/members/$MB/accept" '{}' "$TB" >/dev/null
IB=$(gbody "/api/studio/memberships" "$BTOK" | tr '{' '\n' | grep "\"email\":\"$e2\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/')
CTB=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"defaultDurationMinutes\":60}" "$BTOK")" | jget id)
gB(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$BTOK")" | jget id; }
addlineB(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CTB\",\"instructorProfileId\":\"$IB\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$2,\"pricePerLesson\":$3,\"makeupAllowance\":1,\"dueDate\":\"$4\",\"currency\":\"TRY\",\"preferredSlots\":$SLOTS}" "$BTOK")" | jget id; }
payB(){ body "$(post "/api/studio/students/$1/payments" "{\"amount\":$3,\"studentContractLineId\":\"$2\"}" "$BTOK")" | jget id; }
GX=$(gB GX45); LX=$(addlineB "$GX" 2 500 "$F20")   # total 1000, vade GELECEK, odemesiz -> remaining 1000, overdue 0
GY=$(gB GY45); LY=$(addlineB "$GY" 3 500 "$YDAY")  # total 1500, vade GECMIS -> pay 400 -> remaining 1100, overdue 1100
payB "$GY" "$LY" 400 >/dev/null

DASH=$(gbody "/api/studio/dashboard?month=$MONTHNOW" "$BTOK")
expectn "G: dashboard payments.paidAmount = bu ay alinan (400)" 400 "$(jnum "$DASH" paidAmount)"
expectn "G: dashboard payments.paidCount 1" 1 "$(jnum "$DASH" paidCount)"
expectn "G: dashboard payments.pendingAmount = line-bazli kalan-vadesi-gelmemis (1000)" 1000 "$(jnum "$DASH" pendingAmount)"
expectn "G: dashboard payments.overdueAmount = line-bazli gecikmis (1100)" 1100 "$(jnum "$DASH" overdueAmount)"
expectn "G: dashboard payments.overdueCount 1 (yalniz GY)" 1 "$(jnum "$DASH" overdueCount)"

REP=$(gbody "/api/studio/reports/payments?month=$MONTHNOW" "$BTOK")
expectn "H: reports totals.paidAmount 400 (bu ay alinan)" 400 "$(jnum "$REP" paidAmount)"
expectn "H: reports totals.pendingAmount 1000 (line-bazli)" 1000 "$(jnum "$REP" pendingAmount)"
expectn "H: reports totals.overdueAmount 1100 (line-bazli)" 1100 "$(jnum "$REP" overdueAmount)"
expectn "H: reports payments listesi = 1 (yalniz alinan odeme)" 1 "$(echo "$REP" | grep -o '"status":"Paid"' | wc -l | tr -d ' ')"

echo "--- I) Coklu para birimi grouping (Sprint 41 korunuyor; cross-currency toplam YOK) ---"
GZ=$(gB GZ45)
LZT=$(addlineB "$GZ" 4 500 "$YDAY")                # TRY: total 2000, vade GECMIS -> pay 500 -> remaining 1500, overdue 1500
payB "$GZ" "$LZT" 500 >/dev/null
LZE=$(body "$(post "/api/studio/students/$GZ/contract-lines" "{\"classTypeId\":\"$CTB\",\"instructorProfileId\":\"$IB\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":1,\"pricePerLesson\":300,\"makeupAllowance\":1,\"dueDate\":\"$F20\",\"currency\":\"EUR\",\"preferredSlots\":$SLOTS}" "$BTOK")" | jget id)  # EUR: total 300, vade GELECEK
DEBT=$(gbody "/api/studio/students/$GZ/debt" "$BTOK")
TRYOBJ=$(echo "$DEBT" | grep -o '{"currency":"TRY"[^}]*}')
EUROBJ=$(echo "$DEBT" | grep -o '{"currency":"EUR"[^}]*}')
expectn "I: TRY remaining 1500" 1500 "$(jnum "$TRYOBJ" remaining)"
expectn "I: TRY overdue 1500" 1500 "$(jnum "$TRYOBJ" overdue)"
expectn "I: EUR remaining 300 (TRY'den ETKILENMEDI)" 300 "$(jnum "$EUROBJ" remaining)"
expectn "I: EUR overdue 0 (vade gelecek)" 0 "$(jnum "$EUROBJ" overdue)"

echo "--- J) Reminder hala line.DueDate kullaniyor + yeni Payment artik PaymentDueSoon/Overdue URETMIYOR ---"
mkuserB(){ local g="$1" em="$2"; post /api/auth/register "{\"fullName\":\"${1}u\",\"email\":\"$em\",\"password\":\"$PW\"}" >/dev/null; vrf "$em"; post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$em\",\"initialPassword\":\"$PW\"}" "$BTOK" >/dev/null; login "$em"; }
GW=$(gB GW45); GWTOK=$(mkuserB "$GW" "gw45+$TS@pf.local")
LW=$(addlineB "$GW" 2 500 "$YDAY")   # total 1000, vade GECMIS, odemesiz -> remaining 1000 -> LinePaymentDue beklenir
pcode /api/studio/notifications/run-reminders '{}' "$BTOK" >/dev/null
expect "J: ReminderScanner line.DueDate ile calisiyor -> LinePaymentDue uretildi" yes "$(echo "$(gbody /api/notifications "$GWTOK")" | grep -q 'LinePaymentDue' && echo yes || echo no)"
# Ayni ogrenciye TAM odeme (yeni Payment=Paid akisi) -> remaining 0; regresyon: yeni odeme HICBIR ZAMAN Pending olmadigindan PaymentDueSoon/Overdue uretilmez.
payB "$GW" "$LW" 1000 >/dev/null
pcode /api/studio/notifications/run-reminders '{}' "$BTOK" >/dev/null
NGW=$(gbody /api/notifications "$GWTOK")
expect "J: yeni Payment=Paid akisinda PaymentDueSoon HIC uretilmedi" no "$(echo "$NGW" | grep -q '"type":"PaymentDueSoon"' && echo yes || echo no)"
expect "J: yeni Payment=Paid akisinda PaymentOverdue HIC uretilmedi" no "$(echo "$NGW" | grep -q '"type":"PaymentOverdue"' && echo yes || echo no)"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 45 SMOKE: TUM TESTLER GECTI"; exit 0; } || { echo "SPRINT 45 SMOKE: BASARISIZ"; exit 1; }
