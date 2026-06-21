#!/usr/bin/env bash
# Pliaforma — Sprint 23 smoke (Contract Amendment, Extra Lessons & Student Debt):
#  - Borç hesaplanır (stored balance YOK): TotalCharges = Price + Σ AdditionalPrice; Paid = sözleşmeye bağlı Paid
#    ödemeler; Outstanding = TotalCharges − Paid.
#  - Amendment +4 ders +2000 → TotalLessonCount artar, borç artar. Sadece EndDate uzatma → ders hakkı artmaz.
#  - Amendment sonrası program üretiminde EK HAK kullanılabilir (8 değil 12 ders üretilir).
#  - Ödeme sözleşmeye bağlanınca outstanding düşer; ETİKETSİZ ödeme contract borcunu düşürmez.
#  - Amendment immutable (update/delete endpoint yok). Negatif amendment booked altına düşemez (409).
#  - Cancelled sözleşmeye amendment 409. Currency mismatch 400. Yetki: Instructor → 403.
# Postgres container gerekli. Kullanım: scripts/sprint23-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
nexp(){ awk -v a="$2" -v b="$3" 'BEGIN{exit !(a+0==b+0)}' && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ echo "$1" | grep -o "\"$2\":[0-9.]*" | head -1 | sed "s/\"$2\"://"; }
# Sprint 28B: borç LINE-bazlı → byCurrency[cur].field / unassignedByCurrency[cur].paid (envelope .data).
dval(){ echo "$1" | node -e "try{const e=JSON.parse(require('fs').readFileSync(0,'utf8'));const d=e.data||e;const c=(d.byCurrency||[]).find(x=>x.currency==='$2');console.log(c?c['$3']:'')}catch(_){console.log('')}"; }
unasg(){ echo "$1" | node -e "try{const e=JSON.parse(require('fs').readFileSync(0,'utf8'));const d=e.data||e;const c=(d.unassignedByCurrency||[]).find(x=>x.currency==='$2');console.log(c?c.paid:'')}catch(_){console.log('')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
xcode(){ curl -s -o /dev/null -w "%{http_code}" -X "$1" "$BASE$2" ${3:+-H "Authorization: Bearer $3"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
ctot(){ sql "SELECT \"TotalLessonCount\" FROM \"StudentContracts\" WHERE \"Id\"='$1';"; }
cend(){ sql "SELECT \"EndDate\" FROM \"StudentContracts\" WHERE \"Id\"='$1';"; }
debt(){ curl -s "$BASE/api/studio/students/$1/debt" -H "Authorization: Bearer $ATOK"; }
amnd(){ post "/api/studio/contracts/$1/amendments" "$2" "$ATOK"; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S23 $TS\",\"ownerFullName\":\"O\",\"email\":\"o23+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S23 $TS';")
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2TOK=$(login "i1+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
SD=$(date +%F); ED=$(date -d "+74 days" +%F); ED2=$(date -d "$ED +14 days" +%F)
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$SD\",\"endDate\":\"$ED\",\"totalLessonCount\":8,\"makeupAllowance\":2,\"price\":5000,\"isIndividualDefault\":false}" "$ATOK")" | jget id; }

St=$(guest St23); C=$(mkc "$St")

# Sprint 28B: borç LINE-bazlı (line.TotalPrice = 5000). Amendment debt'i ETKİLEMEZ (28A backfill line'a yedirdi).
echo "--- 1) İlk borç (line 5000, 0 ödenen) ---"
D=$(debt "$St")
nexp "TRY total 5000 (line)" 5000 "$(dval "$D" TRY total)"
nexp "TRY remaining 5000" 5000 "$(dval "$D" TRY remaining)"
nexp "TRY paid 0" 0 "$(dval "$D" TRY paid)"

echo "--- 2) Amendment +4 ders +2000 → contract.Total 12 ama BORÇ DEĞİŞMEZ (line 5000; amendment hariç) ---"
A1=$(amnd "$C" "{\"kind\":\"ExtraLessons\",\"additionalLessonCount\":4,\"additionalPrice\":2000,\"description\":\"Ek paket\"}")
expect "amendment → 200" 200 "$(code "$A1")"
nexp "contract TotalLessonCount 12 (header mutasyonu sürer)" 12 "$(ctot "$C")"
D=$(debt "$St")
nexp "TRY total HÂLÂ 5000 (amendment debt'e KATILMAZ)" 5000 "$(dval "$D" TRY total)"
nexp "TRY remaining HÂLÂ 5000" 5000 "$(dval "$D" TRY remaining)"

echo "--- 3) Sadece EndDate uzatma (+14 gün, 0 ders) → ders hakkı ARTMAZ; borç değişmez ---"
A2=$(amnd "$C" "{\"kind\":\"DateExtension\",\"additionalLessonCount\":0,\"newEndDate\":\"$ED2\",\"additionalPrice\":0}")
expect "date-ext amendment → 200" 200 "$(code "$A2")"
nexp "TotalLessonCount hâlâ 12 (artmadı)" 12 "$(ctot "$C")"
expect "EndDate $ED2 oldu" "$ED2" "$(cend "$C")"
nexp "borç değişmedi (5000)" 5000 "$(dval "$(debt "$St")" TRY remaining)"

# Sprint 28C-1: program LINE-bazlı → amendment line'ı ŞİŞİRMEZ; üretim line.LessonCount (8) ile sınırlı.
echo "--- 4) Amendment program üretimini ETKİLEMEZ (line-bazlı: 8 ders; ek hak yeni line gerektirir) ---"
post "/api/studio/contracts/$C/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"18:00:00\"},{\"dayOfWeek\":\"Thursday\",\"startTime\":\"18:00:00\"}],\"reuseExistingGroup\":true,\"individual\":null}" "$ATOK" >/dev/null
nexp "8 Regular booking üretildi (amendment line'a yansımaz)" 8 "$(sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractId\"='$C' AND b.\"Status\"='Booked' AND s.\"SessionKind\"='Regular';")"

echo "--- 5) Negatif amendment booked altına DÜŞEMEZ (12 planlı, -5 → 7 < 12) ---"
expect "negatif amendment → 409" 409 "$(pcode "/api/studio/contracts/$C/amendments" "{\"kind\":\"Correction\",\"additionalLessonCount\":-5,\"additionalPrice\":0}" "$ATOK")"
nexp "TotalLessonCount değişmedi (12)" 12 "$(ctot "$C")"

echo "--- 6) Ödeme (line'a bağlı) borç düşer (Sprint 29A: strict payment → line zorunlu) ---"
LN=$(sql "SELECT \"Id\" FROM \"StudentContractLines\" WHERE \"StudentContractId\"='$C' LIMIT 1;")
P1=$(body "$(post "/api/studio/students/$St/payments" "{\"amount\":2000,\"dueDate\":\"$SD\",\"studentContractLineId\":\"$LN\"}" "$ATOK")" | jget id)
pcode "/api/studio/payments/$P1/mark-paid" '{}' "$ATOK" >/dev/null
nexp "TRY remaining 3000 (5000−2000)" 3000 "$(dval "$(debt "$St")" TRY remaining)"
P2=$(body "$(post "/api/studio/students/$St/payments" "{\"amount\":3000,\"dueDate\":\"$SD\",\"studentContractLineId\":\"$LN\"}" "$ATOK")" | jget id)
pcode "/api/studio/payments/$P2/mark-paid" '{}' "$ATOK" >/dev/null
nexp "TRY remaining 0" 0 "$(dval "$(debt "$St")" TRY remaining)"

echo "--- 7) Strict: etiketsiz ödeme API'dan oluşturulamaz (400); legacy etiketsiz kayıt borca SAYILMAZ ---"
expect "etiketsiz ödeme API → 400 (strict 29A)" 400 "$(pcode "/api/studio/students/$St/payments" "{\"amount\":1000,\"dueDate\":\"$SD\"}" "$ATOK")"
# Legacy (pre-29A) etiketsiz Paid ödeme SQL ile simüle → borca sayılmaz, unassigned'da görünür (v1.4 debt davranışı korunur).
sql "INSERT INTO \"Payments\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"Amount\",\"Currency\",\"DueDate\",\"Status\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\") SELECT gen_random_uuid(), \"StudioId\", \"Id\", 1000, 'TRY', CURRENT_DATE, 'Paid', '00000000-0000-0000-0000-000000000000', now(), false FROM \"StudentProfiles\" WHERE \"Id\"='$St';" >/dev/null
D=$(debt "$St")
nexp "TRY remaining hâlâ 0 (etiketsiz sayılmaz)" 0 "$(dval "$D" TRY remaining)"
nexp "unassigned TRY 1000 (bilgi)" 1000 "$(unasg "$D" TRY)"

echo "--- 8) Amendment geçmişi immutable ---"
AHIST=$(curl -s "$BASE/api/studio/contracts/$C/amendments" -H "Authorization: Bearer $ATOK")
nexp "2 amendment kaydı (başarısızlar yazılmadı)" 2 "$(sql "SELECT count(*) FROM \"ContractAmendments\" WHERE \"StudentContractId\"='$C';")"
expect "geçmişte createdByName var" yes "$(has "$AHIST" "createdByName")"
AID=$(echo "$AHIST" | jget id)
expect "amendment DELETE endpoint yok → 404" 404 "$(xcode DELETE "/api/studio/contracts/$C/amendments/$AID" "$ATOK")"
expect "amendment PUT endpoint yok → 404" 404 "$(xcode PUT "/api/studio/contracts/$C/amendments/$AID" "$ATOK")"

echo "--- 9) Currency mismatch → 400 ---"
expect "USD amendment (sözleşme TRY) → 400" 400 "$(pcode "/api/studio/contracts/$C/amendments" "{\"kind\":\"PriceAdjustment\",\"additionalLessonCount\":0,\"additionalPrice\":100,\"currency\":\"USD\"}" "$ATOK")"

echo "--- 10) Cancelled sözleşmeye amendment → 409 ---"
St2=$(guest St23b); C2=$(mkc "$St2")
pcode "/api/studio/contracts/$C2/cancel" '{}' "$ATOK" >/dev/null
expect "cancelled contract amendment → 409" 409 "$(pcode "/api/studio/contracts/$C2/amendments" "{\"kind\":\"ExtraLessons\",\"additionalLessonCount\":1,\"additionalPrice\":0}" "$ATOK")"

echo "--- 11) Yetki ---"
expect "Instructor amendment → 403" 403 "$(pcode "/api/studio/contracts/$C/amendments" "{\"kind\":\"Other\",\"additionalLessonCount\":0,\"additionalPrice\":0}" "$I2TOK")"
expect "Instructor borç görüntüleme → 403" 403 "$(gcode "/api/studio/students/$St/debt" "$I2TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 23 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 23 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
