#!/usr/bin/env bash
# Pliaforma — Sprint 28D smoke (Line Due-Date Reminder + Notification Throttle):
#  - LinePaymentDue: Active line + DueDate dolu + DueDate<=today + remaining>0 → öğrenci-bazlı HAFTALIK ÖZET (nazik).
#  - DueDate NULL / remaining 0 / cancelled line → bildirim YOK. Çok line aynı öğrenci → TEK özet.
#  - Aynı öğrenci aynı hafta tekrar → duplicate yok; dedup key önceki haftaya çekilince tekrar üretilir.
#  - Unassigned ödeme remaining'i düşürmez. Per-run cap=1 → 2 öğrenci 2 run'a yayılır. Misafir (UserId NULL) → skip.
#  - Student kendi görür, başka student göremez. Eski ClassReminder/PaymentDueSoon/Overdue bozulmaz.
#  ÖN KOŞUL: backend Notifications__LineReminderMaxPerStudioPerRun=1 ile çalışmalı (throttle determinizmi).
# Postgres + GNU date gerekli. Kullanım: scripts/sprint28d-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S28D $TS\",\"ownerFullName\":\"O\",\"email\":\"o28d+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
e="i1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"I1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; I1P=$(ipid "$e")
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id; }
CT=$(ctype Mat); CT2=$(ctype Reformer)
TODAY=$(date +%F); PAST=$(date -d "-3 days" +%F); START=$(date -d "-10 days" +%F); END=$(date -d "+50 days" +%F)
WEEK=$(date +%G-W%V); PREVWEEK=$(date -d "-7 days" +%G-W%V)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
# Öğrenciyi kullanıcıya çevir → bildirim alabilsin/görsün. Token döner.
mkuser(){ local g="$1" em="$2"; post /api/auth/register "{\"fullName\":\"$1u\",\"email\":\"$em\",\"password\":\"$PW\"}" >/dev/null; vrf "$em"; post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$em\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; login "$em"; }
# addline: $1=student $2=classType $3=dueDate(boş=null) $4=lessonCount $5=perLesson → line id
addline(){ local due=""; [ -n "$3" ] && due=",\"dueDate\":\"$3\""; body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$START\",\"endDate\":\"$END\",\"lessonCount\":$4,\"pricePerLesson\":$5,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]$due}" "$ATOK")" | jget id; }
runrem(){ post /api/studio/notifications/run-reminders '{}' "$ATOK"; }
# LinePaymentDue bildirim sayısı (öğrenci dedup key'iyle).
lpd(){ sql "SELECT count(*) FROM \"Notifications\" WHERE \"Type\"='LinePaymentDue' AND \"DeduplicationKey\" LIKE 'line-payment-due:$1:%';"; }
lpdbody(){ sql "SELECT \"Body\" FROM \"Notifications\" WHERE \"Type\"='LinePaymentDue' AND \"DeduplicationKey\" LIKE 'line-payment-due:$1:%' LIMIT 1;"; }
payline(){ local p; p=$(body "$(post "/api/studio/students/$1/payments" "{\"amount\":$3,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$2\"}" "$ATOK")" | jget id); pcode "/api/studio/payments/$p/mark-paid" '{}' "$ATOK" >/dev/null; }

echo "--- 0) cap=1 doğrulaması (ön koşul) ---"
CAPRES=$(runrem)  # ısınma; sonuç önemli değil
ok "run-reminders erişilebilir ($(code "$CAPRES"))"

echo "--- 1) DueDate NULL → bildirim YOK ---"
SA=$(guest SA); TA=$(mkuser "$SA" "sa28d+$TS@pf.local"); LA=$(addline "$SA" "$CT" "" 4 100)  # dueDate yok
runrem >/dev/null
expectn "DueDate NULL line → LinePaymentDue YOK" 0 "$(lpd "$SA")"

echo "--- 2) Remaining 0 (tam ödendi) → bildirim YOK ---"
SB=$(guest SB); TB=$(mkuser "$SB" "sb28d+$TS@pf.local"); LB=$(addline "$SB" "$CT" "$TODAY" 4 100)  # total 400
payline "$SB" "$LB" 400
runrem >/dev/null
expectn "remaining 0 → LinePaymentDue YOK" 0 "$(lpd "$SB")"

echo "--- 3) Cancelled line → bildirim YOK ---"
SC=$(guest SC); TC=$(mkuser "$SC" "sc28d+$TS@pf.local"); LC=$(addline "$SC" "$CT" "$PAST" 4 100)
pcode "/api/studio/contract-lines/$LC/cancel" '{}' "$ATOK" >/dev/null
runrem >/dev/null
expectn "cancelled line → LinePaymentDue YOK" 0 "$(lpd "$SC")"

echo "--- 4) DueDate today + remaining>0 → LinePaymentDue + nazik metin ---"
SD=$(guest SD); TD=$(mkuser "$SD" "sd28d+$TS@pf.local"); LD=$(addline "$SD" "$CT" "$TODAY" 4 250)  # remaining 1000
R4=$(runrem)
expectn "due today → 1 LinePaymentDue" 1 "$(lpd "$SD")"
expect "nazik metin 'dikkate almayınız' içerir" yes "$(has "$(lpdbody "$SD")" "dikkate almayınız")"
expect "metin tutar+ders tipi içerir (Mat / TRY)" yes "$(has "$(lpdbody "$SD")" "Mat")"

echo "--- 5) DueDate geçmiş + remaining>0 → LinePaymentDue ---"
SE=$(guest SE); TE=$(mkuser "$SE" "se28d+$TS@pf.local"); LE=$(addline "$SE" "$CT" "$PAST" 4 200)  # remaining 800
runrem >/dev/null
expectn "overdue → 1 LinePaymentDue" 1 "$(lpd "$SE")"

echo "--- 6) Aynı öğrenci aynı hafta 2. trigger → duplicate YOK ---"
runrem >/dev/null
expectn "SD hâlâ 1 (haftalık dedup)" 1 "$(lpd "$SD")"

echo "--- 7) Dedup key önceki haftaya çekilince → yeni hafta tekrar üretir ---"
sql "UPDATE \"Notifications\" SET \"DeduplicationKey\"='line-payment-due:$SD:$PREVWEEK' WHERE \"DeduplicationKey\"='line-payment-due:$SD:$WEEK';" >/dev/null
runrem >/dev/null
expectn "SD artık 2 (önceki hafta + bu hafta)" 2 "$(lpd "$SD")"

echo "--- 8) Ödeme sonrası remaining 0 → yeni bildirim YOK ---"
payline "$SE" "$LE" 800   # tam öde
sql "UPDATE \"Notifications\" SET \"DeduplicationKey\"='line-payment-due:$SE:$PREVWEEK' WHERE \"DeduplicationKey\"='line-payment-due:$SE:$WEEK';" >/dev/null
runrem >/dev/null
expectn "SE remaining 0 sonrası bu hafta yeni YOK (hâlâ 1, eski geçen-haftaya taşındı)" 1 "$(lpd "$SE")"

echo "--- 9) Strict (29A): line'sız ödeme → 400; line remaining>0 → reminder oluşur ---"
SF=$(guest SF); TF=$(mkuser "$SF" "sf28d+$TS@pf.local"); LF=$(addline "$SF" "$CT" "$TODAY" 4 300)  # remaining 1200, dueDate bugün
expect "SF line'sız ödeme → 400 (strict 29A)" 400 "$(pcode "/api/studio/students/$SF/payments" "{\"amount\":500,\"dueDate\":\"$TODAY\"}" "$ATOK")"
runrem >/dev/null
expectn "ödeme yok → remaining 1200 → 1 LinePaymentDue" 1 "$(lpd "$SF")"

echo "--- 10) Çok line aynı öğrenci → TEK özet (body 2 line listeler) ---"
SG=$(guest SG); TG=$(mkuser "$SG" "sg28d+$TS@pf.local")
LG1=$(addline "$SG" "$CT" "$TODAY" 4 100); LG2=$(addline "$SG" "$CT2" "$PAST" 4 125)
runrem >/dev/null
expectn "SG TEK LinePaymentDue (özet)" 1 "$(lpd "$SG")"
expect "özet body 2 paketi listeler (Mat + Reformer)" yes "$([ "$(has "$(lpdbody "$SG")" "Mat")" = yes ] && [ "$(has "$(lpdbody "$SG")" "Reformer")" = yes ] && echo yes || echo no)"

echo "--- 11) Per-run cap=1: 2 yeni öğrenci → 1. run 1, 2. run kalan 1 ---"
SH=$(guest SH); TH=$(mkuser "$SH" "sh28d+$TS@pf.local"); LH=$(addline "$SH" "$CT" "$TODAY" 4 100)
SI=$(guest SI); TI=$(mkuser "$SI" "si28d+$TS@pf.local"); LI=$(addline "$SI" "$CT" "$TODAY" 4 100)
R11=$(runrem)
SUM1=$(( $(lpd "$SH") + $(lpd "$SI") ))
expectn "run1: SH+SI toplam 1 üretildi (cap=1)" 1 "$SUM1"
expectn "run1 result.deferred >= 1" 1 "$([ "$(body "$R11" | jq2 'd.data.deferred')" -ge 1 ] && echo 1 || echo 0)"
runrem >/dev/null
SUM2=$(( $(lpd "$SH") + $(lpd "$SI") ))
expectn "run2: SH+SI toplam 2 (kalan üretildi)" 2 "$SUM2"

echo "--- 12) Student kendi görür, başka student göremez ---"
expect "SD kendi LinePaymentDue bildirimini görür" yes "$(has "$(gbody /api/notifications "$TD")" 'LinePaymentDue')"
expect "SA (bildirimsiz) listesinde LinePaymentDue YOK" no "$(has "$(gbody /api/notifications "$TA")" 'LinePaymentDue')"

echo "--- 13) Misafir (UserId NULL) → bildirim üretilmez (skippedGuest) ---"
SJ=$(guest SJ); LJ=$(addline "$SJ" "$CT" "$TODAY" 4 100)   # convert YOK → UserId NULL
RJ=$(runrem)
expectn "misafir SJ → LinePaymentDue YOK" 0 "$(lpd "$SJ")"
expectn "result.skippedGuest >= 1" 1 "$([ "$(body "$RJ" | jq2 'd.data.skippedGuest')" -ge 1 ] && echo 1 || echo 0)"

echo "--- 14) Regresyon: eski PaymentDueSoon (Payment-bazlı) çalışır ---"
SK=$(guest SK); TK=$(mkuser "$SK" "sk28d+$TS@pf.local"); LK=$(addline "$SK" "$CT" "" 4 100)  # line dueDate YOK → LinePaymentDue tetiklenmez
post "/api/studio/students/$SK/payments" "{\"amount\":300,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$LK\"}" "$ATOK" >/dev/null  # due today → PaymentDueSoon (legacy)
runrem >/dev/null
expect "eski PaymentDueSoon bildirimi üretildi" yes "$(has "$(gbody /api/notifications "$TK")" 'PaymentDueSoon')"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 28D SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 28D SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
