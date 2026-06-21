#!/usr/bin/env bash
# Pliaforma — Sprint 28C-2 smoke (Makeup Ledger / Request / Booking LINE-Based):
#  - Manuel grant lineId zorunlu (yoksa 400); doğru line'a yazar. AbsentEarned line A'ya; NULL-line booking kredi üretmez.
#  - line A makeup cap dolunca line A'ya yeni kredi yok; line B etkilenmez. Balance + pending reserve LINE bazlı.
#  - Makeup request create lineId ister; instructor yalnız kendi line'ları; kredisiz/classType-mismatch line eligible'da yok;
#    aynı line 2. pending → 409. Approve Use(line,−1); booking.lineId taşır; session.IsIndividual=line.IsIndividual;
#    makeup line.EndDate sonrası approve. Staff direct makeup: tek uygun line fallback, çoklu → 409, classType eşleşir.
#  - Student self byLine. Level filter eligible'da. Regresyon: v1.5.0 regular / v1.4.0 debt / v1.1.2 rol.
# Postgres + node gerekli. Kullanım: scripts/sprint28c2-smoke.sh [BASE]
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
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"${2:+,\"level\":\"$2\"}}" "$ATOK")" | jget id; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S28C2 $TS\",\"ownerFullName\":\"O\",\"email\":\"o28c2+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
invinstr(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invinstr i1); I1TOK=$(login "i1+$TS@pf.local"); I2P=$(invinstr i2)
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id; }
CT=$(ctype Mat); CT2=$(ctype Reformer)
TODAY=$(date +%F); SOON=$(date -d "+2 days" +%F); END=$(date -d "+40 days" +%F); PASTEND=$(date -d "+50 days" +%F)
# addline: $1=student $2=classType $3=isIndividual $4=makeupAllowance (hoca I1P sabit). I2P için addlineI kullan.
addline(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":$4,\"isIndividual\":$3,\"currency\":\"TRY\"}" "$ATOK")" | jget id; }
addlineI(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$3\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":$5,\"isIndividual\":$4,\"currency\":\"TRY\"}" "$ATOK")" | jget id; }
grant(){ pcode "/api/studio/students/$1/makeup-credits/grant" "{\"studentContractLineId\":\"$2\",\"amount\":$3,\"notes\":\"t\"}" "$ATOK"; }
linebal(){ gbody "/api/studio/students/$1/makeup-credits" "$ATOK" | jq2 "(d.data.byLine.find(l=>l.lineId==='$2')||{}).available"; }
mksession(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"${3}T${4}:00\"${5:+,\"sessionKind\":\"$5\"}}" "$ATOK")" | jget id; }
bookc(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"${3:+,\"studentContractLineId\":\"$3\"}}" "$ATOK"; }
bookid(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"${3:+,\"studentContractLineId\":\"$3\"}}" "$ATOK")" | jget id; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='2024-03-04 10:00:00', \"EndAt\"='2024-03-04 11:00:00' WHERE \"Id\"='$1';" >/dev/null; }
markabsent(){ putc "/api/studio/bookings/$1/attendance" '{"status":"Absent"}' "$ATOK"; }
linecredit(){ sql "SELECT COALESCE(SUM(\"Amount\"),0) FROM \"MakeupCreditTransactions\" WHERE \"StudentContractLineId\"='$1';"; }

echo "--- 1) Manuel grant: lineId yoksa 400; lineId ile doğru line'a yazar ---"
SA=$(guest SA); LA=$(addline "$SA" "$CT" false 1)
expect "grant lineId YOK → 400" 400 "$(pcode "/api/studio/students/$SA/makeup-credits/grant" '{"amount":2,"notes":"x"}' "$ATOK")"
expect "grant lineId ile → 200" 200 "$(grant "$SA" "$LA" 2)"
expectn "LA available=2 (byLine)" 2 "$(linebal "$SA" "$LA")"

echo "--- 2) AbsentEarned line A'ya yazılır + NULL-line booking kredi üretmez ---"
SB=$(guest SB); LB=$(addline "$SB" "$CT" false 1)
SES1=$(mksession "$I1P" "$CT" "$SOON" "12"); B1=$(bookid "$SES1" "$SB")
backdate "$SES1"; expect "Absent işaretle → 200" 200 "$(markabsent "$B1")"
expectn "LB'ye AbsentEarned +1 yazıldı" 1 "$(linecredit "$LB")"
# NULL-line booking → Absent → kredi YOK
SES2=$(mksession "$I1P" "$CT" "$SOON" "13"); B2=$(bookid "$SES2" "$SB")
sql "UPDATE \"ClassBookings\" SET \"StudentContractLineId\"=NULL WHERE \"Id\"='$B2';" >/dev/null
NETBEFORE=$(sql "SELECT COALESCE(SUM(\"Amount\"),0) FROM \"MakeupCreditTransactions\" WHERE \"StudentProfileId\"='$SB';")
backdate "$SES2"; markabsent "$B2" >/dev/null
NETAFTER=$(sql "SELECT COALESCE(SUM(\"Amount\"),0) FROM \"MakeupCreditTransactions\" WHERE \"StudentProfileId\"='$SB';")
expectn "NULL-line Absent kredi ÜRETMEDİ (net değişmedi)" "$NETBEFORE" "$NETAFTER"

echo "--- 3) line A cap dolunca line A yeni kredi yok; line B etkilenmez ---"
SC=$(guest SC); LCA=$(addline "$SC" "$CT" false 1); LCB=$(addline "$SC" "$CT2" false 1)
S3a=$(mksession "$I1P" "$CT" "$SOON" "14"); Ba=$(bookid "$S3a" "$SC")
S3b=$(mksession "$I1P" "$CT" "$SOON" "15"); Bb=$(bookid "$S3b" "$SC")
backdate "$S3a"; markabsent "$Ba" >/dev/null
backdate "$S3b"; markabsent "$Bb" >/dev/null
expectn "LCA cap=1 → yalnız 1 AbsentEarned (2. ders cap dolu)" 1 "$(linecredit "$LCA")"
S3c=$(mksession "$I1P" "$CT2" "$SOON" "16"); Bc=$(bookid "$S3c" "$SC")
backdate "$S3c"; markabsent "$Bc" >/dev/null
expectn "LCB (ayrı line) kendi cap'inden +1 aldı (etkilenmedi)" 1 "$(linecredit "$LCB")"

echo "--- 4) Pending reserve LINE bazlı: line A pending → line B available düşmez ---"
SD=$(guest SD); LDA=$(addline "$SD" "$CT" false 1); LDB=$(addline "$SD" "$CT2" false 1)
grant "$SD" "$LDA" 1 >/dev/null; grant "$SD" "$LDB" 1 >/dev/null
FUT=$(date -d "+5 days" +%F)
REQA=$(body "$(post "/api/studio/makeup-requests" "{\"studentContractLineId\":\"$LDA\",\"startAt\":\"${FUT}T09:00:00\"}" "$ATOK")" | jget id)
expectn "LDA available 0 (pending rezerve)" 0 "$(linebal "$SD" "$LDA")"
expectn "LDB available HÂLÂ 1 (etkilenmez)" 1 "$(linebal "$SD" "$LDB")"

echo "--- 5) Makeup request: lineId zorunlu + aynı line 2. pending 409 ---"
expect "create lineId YOK → 400" 400 "$(pcode "/api/studio/makeup-requests" "{\"startAt\":\"${FUT}T09:00:00\"}" "$ATOK")"
expect "aynı line (LDA) 2. pending → 409" 409 "$(pcode "/api/studio/makeup-requests" "{\"studentContractLineId\":\"$LDA\",\"startAt\":\"${FUT}T15:00:00\"}" "$ATOK")"

echo "--- 6) Eligible: instructor yalnız kendi line'ları; kredisiz line filtre ---"
SE=$(guest SE); LE1=$(addline "$SE" "$CT" false 1); LE2=$(addline "$SE" "$CT" false 1)
grant "$SE" "$LE1" 1 >/dev/null   # LE1 kredili, LE2 kredisiz
ELIG_OWN=$(gbody "/api/studio/makeup-requests/eligible-students" "$ATOK")
expect "kredili LE1 eligible'da var" yes "$(has "$ELIG_OWN" "$LE1")"
expect "kredisiz LE2 eligible'da YOK" no "$(has "$ELIG_OWN" "$LE2")"
# I2P line (başka hoca) kredili → I1 token görmemeli
SF=$(guest SF); LF2=$(addlineI "$SF" "$CT" "$I2P" false 1); grant "$SF" "$LF2" 1 >/dev/null
ELIG_I1=$(gbody "/api/studio/makeup-requests/eligible-students" "$I1TOK")
expect "I1 token: kendi I1P line (LE1) görünür" yes "$(has "$ELIG_I1" "$LE1")"
expect "I1 token: başka hoca (I2P) line (LF2) GÖRÜNMEZ" no "$(has "$ELIG_I1" "$LF2")"

echo "--- 7) Approve: Use(line,−1) + booking.lineId + session.IsIndividual=line.IsIndividual ---"
# Group line approve → group makeup
expect "REQA approve → 200" 200 "$(pcode "/api/studio/makeup-requests/$REQA/approve" '{}' "$ATOK")"
expectn "LDA bakiye 0'a düştü (Use −1)" 0 "$(linecredit "$LDA")"
RSES=$(sql "SELECT \"ResultingClassSessionId\" FROM \"MakeupRequests\" WHERE \"Id\"='$REQA';")
RBOOK=$(sql "SELECT \"ResultingBookingId\" FROM \"MakeupRequests\" WHERE \"Id\"='$REQA';")
expectn "approved booking lineId=LDA taşır" 1 "$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"Id\"='$RBOOK' AND \"StudentContractLineId\"='$LDA';")"
expect "group line → makeup session IsIndividual=false" f "$(sql "SELECT \"IsIndividual\" FROM \"ClassSessions\" WHERE \"Id\"='$RSES';")"

echo "--- 8) Individual line → makeup session individual; makeup line.EndDate sonrası approve ---"
SG=$(guest SG); LG=$(addline "$SG" "$CT" true 1); grant "$SG" "$LG" 1 >/dev/null
REQG=$(body "$(post "/api/studio/makeup-requests" "{\"studentContractLineId\":\"$LG\",\"startAt\":\"${PASTEND}T09:00:00\"}" "$ATOK")" | jget id)  # PASTEND > END (line bitişi sonrası)
expect "makeup line.EndDate sonrası talep+approve → 200" 200 "$(pcode "/api/studio/makeup-requests/$REQG/approve" '{}' "$ATOK")"
GSES=$(sql "SELECT \"ResultingClassSessionId\" FROM \"MakeupRequests\" WHERE \"Id\"='$REQG';")
expect "individual line → makeup session IsIndividual=true" t "$(sql "SELECT \"IsIndividual\" FROM \"ClassSessions\" WHERE \"Id\"='$GSES';")"

echo "--- 9) Staff direct makeup booking: classType eşleşir; tek-fallback / çoklu-409 ---"
SH=$(guest SH); LH1=$(addline "$SH" "$CT" false 1); grant "$SH" "$LH1" 1 >/dev/null
MS=$(mksession "$I1P" "$CT" "$SOON" "17" "Makeup")
expect "tek uygun line → lineId'siz makeup booking 200 (fallback)" 200 "$(bookc "$MS" "$SH")"
# çoklu uygun line (aynı classType CT, ikisi de kredili)
SI=$(guest SI); LI1=$(addline "$SI" "$CT" false 1); LI2=$(addline "$SI" "$CT" false 1)
grant "$SI" "$LI1" 1 >/dev/null; grant "$SI" "$LI2" 1 >/dev/null
MS2=$(mksession "$I1P" "$CT" "$SOON" "18" "Makeup")
expect "çoklu uygun line → lineId'siz makeup booking 409" 409 "$(bookc "$MS2" "$SI")"
expect "lineId verilince → 200" 200 "$(bookc "$MS2" "$SI" "$LI1")"
# classType mismatch: CT2 line, ama CT makeup seansı → o line uygun değil
SJ=$(guest SJ); LJ=$(addline "$SJ" "$CT2" false 1); grant "$SJ" "$LJ" 1 >/dev/null
MS3=$(mksession "$I1P" "$CT" "$SOON" "19" "Makeup")
expect "classType mismatch (CT2 line, CT makeup) → 409" 409 "$(bookc "$MS3" "$SJ")"

echo "--- 10) Student self byLine + level filter eligible ---"
SK=$(guest SK Beginner); LK=$(addline "$SK" "$CT" false 1); grant "$SK" "$LK" 1 >/dev/null
SKMAIL="sk28c2+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"SK\",\"email\":\"$SKMAIL\",\"password\":\"$PW\"}" >/dev/null; vrf "$SKMAIL"
post "/api/studio/students/$SK/convert-to-user" "{\"email\":\"$SKMAIL\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
SKTOK=$(login "$SKMAIL")
MINE=$(gbody "/api/student/makeup-credits" "$SKTOK")
expect "student self byLine LK içerir" yes "$(has "$MINE" "$LK")"
expect "level=Beginner eligible SK line (LK) içerir" yes "$(has "$(gbody "/api/studio/makeup-eligible-students?level=Beginner" "$ATOK")" "$LK")"
expect "level=Advanced eligible LK İÇERMEZ" no "$(has "$(gbody "/api/studio/makeup-eligible-students?level=Advanced" "$ATOK")" "$LK")"

echo "--- 11) Regresyon: v1.5.0 regular booking / v1.4.0 debt / v1.1.2 rol ---"
SL=$(guest SL); LL=$(addline "$SL" "$CT" false 1)
RSESS=$(mksession "$I1P" "$CT" "$SOON" "20")
expect "regular booking (lineId çözümü) → 200" 200 "$(bookc "$RSESS" "$SL")"
PAY=$(body "$(post "/api/studio/students/$SL/payments" "{\"amount\":300,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$LL\"}" "$ATOK")" | jget id)
pcode "/api/studio/payments/$PAY/mark-paid" '{}' "$ATOK" >/dev/null
expectn "LL paid=300 (debt korunur)" 300 "$(gbody "/api/studio/students/$SL/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LL')?.paid")"
expect "Instructor manuel grant → 403 (rol)" 403 "$(pcode "/api/studio/students/$SL/makeup-credits/grant" "{\"studentContractLineId\":\"$LL\",\"amount\":1}" "$I1TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 28C-2 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 28C-2 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
