#!/usr/bin/env bash
# Pliaforma — Sprint 28B smoke (Line Payments/Debt + Contract Line Editor):
#  - Yeni line (ek alım); aynı classType 2. alım yeni line (eski şişmez).
#  - Line A ödemesi yalnız A'yı etkiler; B etkilenmez.
#  - Line'sız ödeme "Diğer ödeme" (borca sayılmaz). ContractId-only legacy: YALNIZ tek-line header'da fallback;
#    çok-line header'da null-line ödeme HİÇBİR line'a sayılmaz (çift sayım yok).
#  - Amendment debt'e KATILMAZ. Currency bazında ayrı özet; cross-currency tek toplam yok. Currency uyuşmazlık → 400.
#  - DueDate/Notes edit; güvenli cancel (paid varsa 409). Instructor erişemez; Student yazmaz, kendi line/debt okur.
# Postgres + node gerekli. Kullanım: scripts/sprint28b-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
# JSON path çıkarıcı (ApiResponse envelope → d.data). $1 = JS ifade (d kök).
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
put(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S28B $TS\",\"ownerFullName\":\"O\",\"email\":\"o28b+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
e="i1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"I1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; I1P=$(ipid "$e"); I1TOK=$(login "$e")
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id; }
CT1=$(ctype Mat); CT2=$(ctype Reformer)
TODAY=$(date +%F); END=$(date -d "+60 days" +%F)
SX=$(guest SX); SY=$(guest SY)
# addline: $1=student $2=ct $3=lessonCount $4=perLesson $5=currency $6=extra-json → full post çıktısı
addline(){ post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$3,\"pricePerLesson\":$4,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"$5\"${6:+,$6}}" "$ATOK"; }
markpaid(){ pcode "/api/studio/payments/$1/mark-paid" '{}' "$ATOK"; }
paid(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.paid"; }
remaining(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.remaining"; }

echo "--- 1) Yeni line + aynı classType 2. alım = yeni line (eski şişmez) ---"
RL1=$(addline "$SX" "$CT1" 10 100 TRY); expect "L1 ekle (10×100 TRY) → 200" 200 "$(code "$RL1")"
L1=$(body "$RL1" | jget id); H1=$(body "$RL1" | jget studentContractId)
expect "L1 totalPrice=1000" yes "$(has "$(body "$RL1")" '"totalPrice":1000')"
expect "L1 dueDate YOK (NULL→omit)" no "$(has "$(body "$RL1")" '"dueDate":"')"
RL2=$(addline "$SX" "$CT1" 10 100 TRY); L2=$(body "$RL2" | jget id)
expect "L2 (aynı classType) ekle → 200" 200 "$(code "$RL2")"
LINES=$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK")
expectn "SX'te 2 line" 2 "$(echo "$LINES" | jq2 'd.data.length')"
expectn "L1 lessonCount hâlâ 10 (şişmedi)" 10 "$(echo "$LINES" | jq2 "d.data.find(l=>l.id==='$L1')?.lessonCount")"
expect "L1 ve L2 farklı (yeni line)" no "$([ "$L1" = "$L2" ] && echo yes || echo no)"

echo "--- 2) Line A ödemesi yalnız A'yı etkiler; B etkilenmez ---"
P1=$(body "$(post "/api/studio/students/$SX/payments" "{\"amount\":300,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$L1\"}" "$ATOK")" | jget id)
markpaid "$P1" >/dev/null
expectn "L1 paid=300" 300 "$(paid "$SX" "$L1")"; expectn "L1 remaining=700" 700 "$(remaining "$SX" "$L1")"
expectn "L2 paid=0 (etkilenmedi)" 0 "$(paid "$SX" "$L2")"; expectn "L2 remaining=1000" 1000 "$(remaining "$SX" "$L2")"

echo "--- 3) Currency uyuşmazlık → 400 ---"
expect "TRY line'a USD ödeme → 400" 400 "$(pcode "/api/studio/students/$SX/payments" "{\"amount\":10,\"currency\":\"USD\",\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$L1\"}" "$ATOK")"

echo "--- 4) Amendment debt'e KATILMAZ ---"
pcode "/api/studio/contracts/$H1/amendments" "{\"kind\":\"PriceAdjustment\",\"additionalLessonCount\":0,\"additionalPrice\":500}" "$ATOK" >/dev/null
DEBT=$(gbody "/api/studio/students/$SX/debt" "$ATOK")
expectn "TRY borç toplam=2000 (L1+L2; amendment 500 HARİÇ)" 2000 "$(echo "$DEBT" | jq2 "d.data.byCurrency.find(c=>c.currency==='TRY')?.total")"

echo "--- 5) Çok-line header'da contractId-only null-line ödeme HİÇBİR line'a sayılmaz (çift sayım yok) ---"
P2=$(body "$(post "/api/studio/students/$SX/payments" "{\"amount\":400,\"dueDate\":\"$TODAY\",\"studentContractId\":\"$H1\"}" "$ATOK")" | jget id)
markpaid "$P2" >/dev/null
expectn "L1 paid hâlâ 300 (400 dağılmadı)" 300 "$(paid "$SX" "$L1")"
expectn "L2 paid hâlâ 0" 0 "$(paid "$SX" "$L2")"
DEBT=$(gbody "/api/studio/students/$SX/debt" "$ATOK")
expectn "TRY ödenen=300 (yalnız L1; çok-line header null-line sayılmaz)" 300 "$(echo "$DEBT" | jq2 "d.data.byCurrency.find(c=>c.currency==='TRY')?.paid")"
expectn "TRY atanmamış=400" 400 "$(echo "$DEBT" | jq2 "d.data.unassignedByCurrency.find(c=>c.currency==='TRY')?.paid")"

echo "--- 6) Line'sız ödeme = Diğer ödeme (borca sayılmaz) ---"
P3=$(body "$(post "/api/studio/students/$SX/payments" "{\"amount\":50,\"dueDate\":\"$TODAY\"}" "$ATOK")" | jget id)
markpaid "$P3" >/dev/null
DEBT=$(gbody "/api/studio/students/$SX/debt" "$ATOK")
expectn "TRY atanmamış=450 (400+50)" 450 "$(echo "$DEBT" | jq2 "d.data.unassignedByCurrency.find(c=>c.currency==='TRY')?.paid")"
expectn "TRY ödenen hâlâ 300 (borca sayılmaz)" 300 "$(echo "$DEBT" | jq2 "d.data.byCurrency.find(c=>c.currency==='TRY')?.paid")"

echo "--- 7) Currency bazında ayrı (USD line); cross-currency tek toplam yok ---"
RU=$(addline "$SX" "$CT1" 5 20 USD); LU=$(body "$RU" | jget id)
expect "USD line ekle → 200" 200 "$(code "$RU")"
DEBT=$(gbody "/api/studio/students/$SX/debt" "$ATOK")
expectn "byCurrency 2 blok (TRY+USD)" 2 "$(echo "$DEBT" | jq2 'd.data.byCurrency.length')"
expectn "USD total=100 (5×20)" 100 "$(echo "$DEBT" | jq2 "d.data.byCurrency.find(c=>c.currency==='USD')?.total")"

echo "--- 8) ContractId-only legacy: tek-line header'da fallback sayılır (SY) ---"
RY=$(addline "$SY" "$CT1" 8 50 TRY); LY=$(body "$RY" | jget id); HY=$(body "$RY" | jget studentContractId)
PY=$(body "$(post "/api/studio/students/$SY/payments" "{\"amount\":200,\"dueDate\":\"$TODAY\",\"studentContractId\":\"$HY\"}" "$ATOK")" | jget id)
markpaid "$PY" >/dev/null
expectn "SY tek-line header → L_Y paid=200 (legacy fallback)" 200 "$(paid "$SY" "$LY")"

echo "--- 9) DueDate/Notes edit + güvenli cancel ---"
expect "L2 dueDate/notes edit → 200" 200 "$(code "$(put "/api/studio/contract-lines/$L2" "{\"dueDate\":\"2026-12-31\",\"notes\":\"x\"}" "$ATOK")")"
expect "L2 dueDate güncellendi" yes "$(has "$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK")" '"dueDate":"2026-12-31"')"
expect "L2 (ödemesiz) cancel → 200" 200 "$(pcode "/api/studio/contract-lines/$L2/cancel" '{}' "$ATOK")"
expect "L1 (ödenmiş ödeme var) cancel → 409" 409 "$(pcode "/api/studio/contract-lines/$L1/cancel" '{}' "$ATOK")"

echo "--- 10) Yetki: Instructor erişemez; Student yazmaz, kendi line/debt okur ---"
expect "Instructor line ekle → 403" 403 "$(pcode "/api/studio/students/$SX/contract-lines" "{\"classTypeId\":\"$CT1\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":1,\"pricePerLesson\":1,\"makeupAllowance\":0,\"isIndividual\":false}" "$I1TOK")"
expect "Instructor ödeme ekle → 403" 403 "$(pcode "/api/studio/students/$SX/payments" "{\"amount\":1,\"dueDate\":\"$TODAY\"}" "$I1TOK")"
post "/api/studio/students/$SX/convert-to-user" "{\"email\":\"sx+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
STOK=$(login "sx+$TS@pf.local")
expect "Student kendi line'larını okur → 200" 200 "$(gcode /api/student/contract-lines "$STOK")"
expect "Student kendi borcunu okur → 200" 200 "$(gcode /api/student/debt "$STOK")"
expect "Student line ekleyemez (studio uç) → 403" 403 "$(pcode "/api/studio/students/$SX/contract-lines" "{\"classTypeId\":\"$CT1\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":1,\"pricePerLesson\":1,\"makeupAllowance\":0,\"isIndividual\":false}" "$STOK")"
expect "Student my-payments line etiketi içerir (lineLabel)" yes "$(has "$(gbody /api/student/payments "$STOK")" '"lineLabel":"')"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 28B SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 28B SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
