#!/usr/bin/env bash
# Pliaforma — Sprint 38 smoke (Ödeme planı / taksit takibi):
#  A. CRUD: 3 taksit PUT → GET installment; peşin (1 taksit) → Cash; Σ!=TotalPrice → 400; amount 0 → 400.
#  B. Allocation/status: ödeme yok → geçmiş vade Overdue; 3000 öde → 1 Paid + 1 Partial; fazla öde → overpaidAmount.
#  C. Overdue: today==dueDate overdue DEGIL; dueDate<today + eksik → overdue.
#  D. Create line + paymentPlan → installment + line.DueDate = en erken vade; plansız → isConfigured false; Σ!=total → 400.
# Payment entity DEGISMEDI; allocation hesaplanan. ASCII-only. Postgres opsiyonel.
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$2" | grep -q "$3" && ok "$1" || bad "$1 (icermiyor: $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ echo "$1" | grep -o "\"$2\":-\\?[0-9]*" | head -1 | sed "s/\"$2\"://"; }
cnt(){ echo "$1" | grep -o "\"status\":\"$2\"" | wc -l | tr -d ' '; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
vrf(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
TS=$(date +%s)
TODAY=$(date +%F); YDAY=$(date -d "-1 day" +%F); PAST=$(date -d "-10 days" +%F)
F20=$(date -d "+20 days" +%F); F50=$(date -d "+50 days" +%F); END=$(date -d "+200 days" +%F)
ipid(){ gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 38: odeme plani / taksit =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S38 $TS\",\"ownerFullName\":\"O\",\"email\":\"o38+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 2; }
e="hocaA+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"hocaA\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null
IA=$(ipid "$e")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
SLOTS='[{"dayOfWeek":"Monday","preferredStartTime":null,"notes":null}]'
# addline $student $lessonCount $price → line id (total = count*price)
addline(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$IA\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$2,\"pricePerLesson\":$3,\"makeupAllowance\":1,\"currency\":\"TRY\",\"preferredSlots\":$SLOTS}" "$ATOK")" | jget id; }
addlinePlanRaw(){ post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$IA\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$2,\"pricePerLesson\":$3,\"makeupAllowance\":1,\"currency\":\"TRY\",\"preferredSlots\":$SLOTS,\"paymentPlan\":$4}" "$ATOK"; }
INST(){ echo "{\"dueDate\":\"$1\",\"amount\":$2,\"notes\":null}"; }
putplan(){ putc "/api/studio/contract-lines/$1/payment-plan" "{\"installments\":$2}" "$ATOK"; }
getplan(){ gbody "/api/studio/contract-lines/$1/payment-plan" "$ATOK"; }
pay(){ local pid=$(body "$(post "/api/studio/students/$1/payments" "{\"amount\":$3,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$2\"}" "$ATOK")" | jget id); pcode "/api/studio/payments/$pid/mark-paid" '{}' "$ATOK" >/dev/null; }

echo "--- A) Plan CRUD ---"
SA=$(guest A38); LA=$(addline "$SA" 6 1000)   # total 6000
P3="[$(INST $TODAY 2000),$(INST $F20 2000),$(INST $F50 2000)]"
expect "A: PUT 3 taksit (Σ6000) → 200" 200 "$(putplan "$LA" "$P3")"
GA=$(getplan "$LA")
has    "A: GET isConfigured true" "$GA" '"isConfigured":true'
has    "A: planType Installment" "$GA" '"planType":"Installment"'
expectn "A: totalPlannedAmount 6000" 6000 "$(jnum "$GA" totalPlannedAmount)"
# pesin
SB=$(guest B38); LB=$(addline "$SB" 6 1000)
expect "A: PUT pesin (1 taksit 6000) → 200" 200 "$(putplan "$LB" "[$(INST $TODAY 6000)]")"
has    "A: pesin planType Cash" "$(getplan "$LB")" '"planType":"Cash"'
# Σ mismatch
expect "A: Σ!=total (5000) → 400" 400 "$(putplan "$LA" "[$(INST $TODAY 2000),$(INST $F20 2000),$(INST $F50 1000)]")"
# amount 0
expect "A: amount 0 (Σ6000 ama 0 var) → 400" 400 "$(putplan "$LA" "[$(INST $TODAY 3000),$(INST $F20 3000),$(INST $F50 0)]")"

echo "--- B) Allocation / status ---"
SC=$(guest C38); LC=$(addline "$SC" 6 1000)    # total 6000
PB="[$(INST $PAST 2000),$(INST $F20 2000),$(INST $F50 2000)]"   # 1. taksit gecmis vade
putplan "$LC" "$PB" >/dev/null
G0=$(getplan "$LC")
expectn "B: odeme yok → overdueCount 1 (gecmis vade)" 1 "$(jnum "$G0" overdueCount)"
expectn "B: overdueAmount 2000" 2000 "$(jnum "$G0" overdueAmount)"
expectn "B: paidCount 0" 0 "$(jnum "$G0" paidCount)"
pay "$SC" "$LC" 3000   # line.Paid=3000
G1=$(getplan "$LC")
expectn "B: 3000 ode → paidCount 1 (en eski taksit)" 1 "$(jnum "$G1" paidCount)"
expectn "B: partialCount 1 (sonraki kismi)" 1 "$(jnum "$G1" partialCount)"
expectn "B: overdueCount 0 (gecmis taksit artik Paid)" 0 "$(jnum "$G1" overdueCount)"
pay "$SC" "$LC" 4000   # toplam 7000 > 6000
G2=$(getplan "$LC")
expectn "B: fazla ode → paidCount 3" 3 "$(jnum "$G2" paidCount)"
expectn "B: overpaidAmount 1000" 1000 "$(jnum "$G2" overpaidAmount)"

echo "--- C) Overdue siniri ---"
SD=$(guest D38); LD=$(addline "$SD" 1 4000)    # total 4000
putplan "$LD" "[$(INST $TODAY 4000)]" >/dev/null   # vade BUGUN
expectn "C: today==dueDate → overdueCount 0" 0 "$(jnum "$(getplan "$LD")" overdueCount)"
SE=$(guest E38); LE=$(addline "$SE" 1 4000)
putplan "$LE" "[$(INST $YDAY 4000)]" >/dev/null    # vade DUN
expectn "C: dueDate<today + eksik → overdueCount 1" 1 "$(jnum "$(getplan "$LE")" overdueCount)"

echo "--- D) Create line + paymentPlan ---"
SF=$(guest F38)
CP="[$(INST $TODAY 2000),$(INST $F20 2000),$(INST $F50 2000)]"
RESP=$(body "$(addlinePlanRaw "$SF" 6 1000 "$CP")")
LF=$(echo "$RESP" | jget id)
expect "D: create line + plan → line olustu" 1 "$([ -n "$LF" ] && echo 1 || echo 0)"
has    "D: create response dueDate = en erken vade ($TODAY)" "$RESP" "\"dueDate\":\"$TODAY\""
has    "D: plan isConfigured true" "$(getplan "$LF")" '"isConfigured":true'
# plansiz line
SG=$(guest G38); LG=$(addline "$SG" 6 1000)
has    "D: plansiz line → isConfigured false" "$(getplan "$LG")" '"isConfigured":false'
has    "D: plansiz line → planType None" "$(getplan "$LG")" '"planType":"None"'
# create + plan Σ mismatch → 400
expect "D: create + plan Σ!=total → 400" 400 "$(code "$(addlinePlanRaw "$SG" 6 1000 "[$(INST $TODAY 2000),$(INST $F20 2000),$(INST $F50 1000)]")")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 38 SMOKE: TUM TESTLER GECTI"; exit 0; } || { echo "SPRINT 38 SMOKE: BASARISIZ"; exit 1; }
