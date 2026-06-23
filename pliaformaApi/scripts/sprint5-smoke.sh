#!/usr/bin/env bash
# Pliaforma — Sprint 5 smoke (erteleme/iptal, telafi kredisi ledger, makeup seans booking, toplu closure,
# yetki + tenant izolasyonu). Kullanım: scripts/sprint5-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":-\?[0-9]*" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
bcredit(){ curl -s "$BASE/api/studio/students/$1/makeup-credits" -H "Authorization: Bearer $ATOK" | jnum balance; }
D=$(date -d "+3 days" +%Y-%m-%d); D2=$(date -d "+5 days" +%Y-%m-%d); D3=$(date -d "+7 days" +%Y-%m-%d); D3b=$(date -d "+9 days" +%Y-%m-%d); D4=$(date -d "+11 days" +%Y-%m-%d)

# --- Stüdyolar + hocalar + grup ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S5A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s5a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S5C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s5c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
for n in i1 i2; do post /api/auth/register "{\"fullName\":\"$n\",\"email\":\"$n+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null; done
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
inv(){ local e="$1+$TS@pf.local"; vrf "$e"; RR=$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); M=$(body "$RR" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
I1P=$(inv i1); I2P=$(inv i2); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":2,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2T$3:00\",\"capacity\":${4:-2},\"sessionKind\":\"${5:-Regular}\"}" "$ATOK")" | jget id; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
mkactive(){ local g; g=$(guest "$1"); post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$1+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; echo "$g"; }
SA=$(mkactive "sa"); SATOK=$(login "sa+$TS@pf.local")
SB=$(mkactive "sb"); SBTOK=$(login "sb+$TS@pf.local")
# Sprint 18b: Regular booking SÖZLEŞME ister (paket yok); self-booking kapalı. mkc → öğrenciye geniş sözleşme.
mkc(){ curl -s -o /dev/null -X POST "$BASE/api/studio/students/$1/contracts" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":100,\"makeupAllowance\":5,\"price\":0,\"isIndividualDefault\":false}"; }
# Sprint 28C-2: manuel grant/revoke lineId zorunlu → öğrencinin line'ını çöz. SA/SB/GA için önce sözleşme (line) gerekir.
glineof(){ sql "SELECT l.\"Id\" FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE c.\"StudentProfileId\"='$1' LIMIT 1;"; }
mkc "$SA"; mkc "$SB"

echo "--- Makeup seansı: kredi yokken 409, grant, kredi tüket, iptalde restore (staff booking) ---"
MS=$(mksess "$I1P" "$D" "09:00" 2 "Makeup")
expect "kredisiz makeup staff-book → 409" 409 "$(pcode "/api/studio/class-sessions/$MS/bookings" "{\"studentProfileId\":\"$SA\"}" "$ATOK")"
GR=$(post "/api/studio/students/$SA/makeup-credits/grant" "{\"studentContractLineId\":\"$(glineof "$SA")\",\"amount\":1,\"notes\":\"test\"}" "$ATOK")
expect "manuel grant → 200" 200 "$(code "$GR")"
expect "grant sonrası bakiye 1" 1 "$(body "$GR" | jnum balance)"
R=$(post "/api/studio/class-sessions/$MS/bookings" "{\"studentProfileId\":\"$SA\"}" "$ATOK")
expect "kredili makeup staff-book → 200" 200 "$(code "$R")"; MBK=$(body "$R" | jget id)
expect "makeup booking sonrası bakiye 0" 0 "$(bcredit "$SA")"
expect "makeup booking iptal → 200" 200 "$(pcode "/api/studio/bookings/$MBK/cancel" '{}' "$ATOK")"
expect "iptal sonrası kredi restore → bakiye 1" 1 "$(bcredit "$SA")"

echo "--- Manuel grant / aylık limit / revoke ---"
expect "SB +2 grant → 200" 200 "$(pcode "/api/studio/students/$SB/makeup-credits/grant" "{\"studentContractLineId\":\"$(glineof "$SB")\",\"amount\":2}" "$ATOK")"
expect "aylık limit (2) aşımı +1 → 409" 409 "$(pcode "/api/studio/students/$SB/makeup-credits/grant" "{\"studentContractLineId\":\"$(glineof "$SB")\",\"amount\":1}" "$ATOK")"
RV=$(post "/api/studio/students/$SB/makeup-credits/revoke" "{\"studentContractLineId\":\"$(glineof "$SB")\",\"amount\":1}" "$ATOK")
expect "revoke → bakiye 1" 1 "$(body "$RV" | jnum balance)"
expect "bakiyeden fazla revoke → 409" 409 "$(pcode "/api/studio/students/$SB/makeup-credits/revoke" "{\"studentContractLineId\":\"$(glineof "$SB")\",\"amount\":5}" "$ATOK")"

echo "--- Regular seans booking kredi tüketmez (staff + sözleşme) ---"
RS=$(mksess "$I1P" "$D" "10:00" 2 "Regular"); mkc "$SB"
expect "SB regular staff-book → 200" 200 "$(pcode "/api/studio/class-sessions/$RS/bookings" "{\"studentProfileId\":\"$SB\"}" "$ATOK")"
expect "regular sonrası SB bakiye değişmedi (1)" 1 "$(bcredit "$SB")"

echo "--- Session cancel + grantMakeupCredit ---"
CS=$(mksess "$I1P" "$D" "11:00" 2 "Regular"); GC=$(guest "GC"); mkc "$GC"
pcode "/api/studio/class-sessions/$CS/bookings" "{\"studentProfileId\":\"$GC\"}" "$ATOK" >/dev/null
expect "cancel + grant → 200" 200 "$(pcode "/api/studio/class-sessions/$CS/cancel" "{\"reason\":\"hoca yok\",\"grantMakeupCredit\":true}" "$ATOK")"
expect "iptal booking sahibine +1 kredi" 1 "$(bcredit "$GC")"
expect "aynı seans tekrar iptal → 409 (çift kredi yok)" 409 "$(pcode "/api/studio/class-sessions/$CS/cancel" "{\"grantMakeupCredit\":true}" "$ATOK")"
expect "tekrar iptal sonrası bakiye hâlâ 1 (Regular grant)" 1 "$(bcredit "$GC")"

echo "--- (A) Makeup cancel + grant ÇİFT KREDİ ENGELİ: sadece restore, ekstra grant yok ---"
GA=$(guest "GA"); mkc "$GA"; pcode "/api/studio/students/$GA/makeup-credits/grant" "{\"studentContractLineId\":\"$(glineof "$GA")\",\"amount\":1}" "$ATOK" >/dev/null
expect "(A) başlangıç bakiye 1" 1 "$(bcredit "$GA")"
MSA=$(mksess "$I1P" "$D" "16:00" 2 "Makeup")
pcode "/api/studio/class-sessions/$MSA/bookings" "{\"studentProfileId\":\"$GA\"}" "$ATOK" >/dev/null
expect "(A) makeup booking sonrası bakiye 0" 0 "$(bcredit "$GA")"
pcode "/api/studio/class-sessions/$MSA/cancel" "{\"grantMakeupCredit\":true}" "$ATOK" >/dev/null
expect "(A) makeup session cancel+grant → bakiye 1 (2 DEĞİL)" 1 "$(bcredit "$GA")"

echo "--- (B) Bulk closure makeup ÇİFT KREDİ ENGELİ: sadece restore ---"
GBk=$(guest "GBk"); mkc "$GBk"; pcode "/api/studio/students/$GBk/makeup-credits/grant" "{\"studentContractLineId\":\"$(glineof "$GBk")\",\"amount\":1}" "$ATOK" >/dev/null
MSB=$(mksess "$I1P" "$D4" "10:00" 2 "Makeup")
pcode "/api/studio/class-sessions/$MSB/bookings" "{\"studentProfileId\":\"$GBk\"}" "$ATOK" >/dev/null
expect "(B) makeup booking sonrası bakiye 0" 0 "$(bcredit "$GBk")"
CLB=$(post /api/studio/closures "{\"title\":\"BulkMakeup\",\"startAt\":\"${D4}T00:00:00\",\"endAt\":\"${D4}T23:59:00\",\"actionType\":\"CancelAndGrantMakeupCredit\"}" "$ATOK")
expect "(B) bulk closure → 200" 200 "$(code "$CLB")"
expect "(B) closure makeup booking için ekstra grant yok (creditedCount=0)" 0 "$(body "$CLB" | jnum creditedCount)"
expect "(B) bulk closure makeup sonrası bakiye 1 (2 DEĞİL)" 1 "$(bcredit "$GBk")"

echo "--- Reschedule (yetki/tenant 404-403 önce, sonra fonksiyonel) ---"
R1=$(mksess "$I1P" "$D" "12:00" 2); G2=$(guest "G2"); mkc "$G2"
pcode "/api/studio/class-sessions/$R1/bookings" "{\"studentProfileId\":\"$G2\"}" "$ATOK" >/dev/null
expect "I2 başka hocanın seansını ertele → 403" 403 "$(pcode "/api/studio/class-sessions/$R1/reschedule" "{\"newStartAt\":\"${D}T20:00:00\",\"keepBookings\":true}" "$I2TOK")"
expect "C başka stüdyo seansını ertele → 404" 404 "$(pcode "/api/studio/class-sessions/$R1/reschedule" "{\"newStartAt\":\"${D}T20:00:00\",\"keepBookings\":true}" "$CTOK")"
expect "reschedule keepBookings=true → 200" 200 "$(pcode "/api/studio/class-sessions/$R1/reschedule" "{\"newStartAt\":\"${D}T20:00:00\",\"reason\":\"tasindi\",\"keepBookings\":true}" "$ATOK")"
KB=$(curl -s "$BASE/api/studio/class-sessions/$R1/bookings" -H "Authorization: Bearer $ATOK" | grep -o '"status":"Booked"' | wc -l)
expect "keepBookings: rezervasyon korundu" 1 "$KB"
R2=$(mksess "$I1P" "$D" "13:00" 2); G3=$(guest "G3"); mkc "$G3"
pcode "/api/studio/class-sessions/$R2/bookings" "{\"studentProfileId\":\"$G3\"}" "$ATOK" >/dev/null
expect "reschedule keepBookings=false → 200" 200 "$(pcode "/api/studio/class-sessions/$R2/reschedule" "{\"newStartAt\":\"${D}T21:00:00\",\"keepBookings\":false}" "$ATOK")"
DROP=$(curl -s "$BASE/api/studio/class-sessions/$R2/bookings" -H "Authorization: Bearer $ATOK" | grep -o '"status":"CancelledByStudio"' | wc -l)
expect "keepBookings=false: rezervasyon iptal edildi" 1 "$DROP"

echo "--- Tekil seans cancel: I2 403 / C 404 ---"
XC=$(mksess "$I1P" "$D" "15:00" 2)
expect "I2 başka hocanın seansını iptal → 403" 403 "$(pcode "/api/studio/class-sessions/$XC/cancel" '{}' "$I2TOK")"
expect "C başka stüdyo seansını iptal → 404" 404 "$(pcode "/api/studio/class-sessions/$XC/cancel" '{}' "$CTOK")"

echo "--- Toplu closure: cancelAndGrant ---"
BC1=$(mksess "$I1P" "$D2" "14:00" 2); BC2=$(mksess "$I1P" "$D2" "15:00" 2)
GB1=$(guest "GB1"); GB2=$(guest "GB2"); mkc "$GB1"; mkc "$GB2"
pcode "/api/studio/class-sessions/$BC1/bookings" "{\"studentProfileId\":\"$GB1\"}" "$ATOK" >/dev/null
pcode "/api/studio/class-sessions/$BC2/bookings" "{\"studentProfileId\":\"$GB2\"}" "$ATOK" >/dev/null
CL=$(post /api/studio/closures "{\"title\":\"Tatil\",\"startAt\":\"${D2}T00:00:00\",\"endAt\":\"${D2}T23:59:00\",\"actionType\":\"CancelAndGrantMakeupCredit\"}" "$ATOK")
expect "closure oluştur → 200" 200 "$(code "$CL")"; CLID=$(body "$CL" | jget id)
expect "closure cancelledCount=2" 2 "$(body "$CL" | jnum cancelledCount)"
expect "closure creditedCount=2" 2 "$(body "$CL" | jnum creditedCount)"
expect "closure sonrası GB1 +1 kredi" 1 "$(bcredit "$GB1")"

echo "--- Toplu reschedule offset: uygun taşınır, çakışan skipped ---"
STG=$(mksess "$I1P" "$D3b" "16:00" 2)   # batch dışı hedef (çakışma yaratır)
SMV=$(mksess "$I1P" "$D3" "16:00" 2)     # +2 gün → D3b 16:00 ile çakışır → skipped
SOK=$(mksess "$I1P" "$D3" "17:00" 2)     # +2 gün → D3b 17:00 boş → rescheduled
CL2=$(post /api/studio/closures "{\"title\":\"Erteleme\",\"startAt\":\"${D3}T00:00:00\",\"endAt\":\"${D3}T23:59:00\",\"actionType\":\"RescheduleByOffset\",\"offsetDays\":2}" "$ATOK")
expect "bulk reschedule → 200" 200 "$(code "$CL2")"
expect "rescheduledCount=1" 1 "$(body "$CL2" | jnum rescheduledCount)"
expect "skippedCount=1 (çakışma)" 1 "$(body "$CL2" | jnum skippedCount)"

echo "--- Tenant + rol: closure / kredi görünürlüğü ---"
expect "C başka stüdyo closure get → 404" 404 "$(gcode "/api/studio/closures/$CLID" "$CTOK")"
expect "C başka stüdyo öğrenci kredisi → 404" 404 "$(gcode "/api/studio/students/$SA/makeup-credits" "$CTOK")"
expect "I2 erişemediği öğrenciye grant → 403" 403 "$(pcode "/api/studio/students/$SA/makeup-credits/grant" "{\"amount\":1}" "$I2TOK")"
expect "Student staff kredi ucu → 403" 403 "$(gcode "/api/studio/students/$SA/makeup-credits" "$SATOK")"
expect "Student closures listesi → 403" 403 "$(gcode /api/studio/closures "$SATOK")"
expect "Student kendi makeup-credits → 200" 200 "$(gcode /api/student/makeup-credits "$SATOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 5 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 5 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
