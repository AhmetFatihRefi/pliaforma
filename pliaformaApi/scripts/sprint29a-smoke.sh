#!/usr/bin/env bash
# Pliaforma — Sprint 29A smoke (Strict Line Payments + Preferred Slot Data Model):
#  - Strict payment: lineId'siz CreatePayment → 400; lineId ile → 200. Eski (legacy) etiketsiz ödeme SQL ile
#    simüle → borca SAYILMAZ, unassigned'da görünür (v1.4 debt davranışı korunur). Tek-line header legacy fallback.
#  - Preferred slot: yeni line ≥1 slot zorunlu (boş → 400); ≥1 slot → 200. Slot ekle/listele/sil; aynı (gün,saat)
#    duplicate → 409 (saatsiz dahil); saatsiz slot eklenebilir. DTO.preferredSlots döner.
#  - /contracts (legacy) slotsuz line üretebilir (geçiş); slotsuz line bozulmaz.
#  - Yetki: Instructor/Student slot yazamaz (403); Owner yazar.
# NOT: Bu yığında tüm doğrulama hataları 400 döner (422 yok).
# Postgres + node gerekli. Kullanım: scripts/sprint29a-smoke.sh [BASE]
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
dcode(){ curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
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

echo "== Sprint 29A: Strict Line Payments + Preferred Slots =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S29A $TS\",\"ownerFullName\":\"O\",\"email\":\"o29a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
e="i1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"I1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; I1P=$(ipid "$e"); I1TOK=$(login "$e")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
TODAY=$(date +%F); END=$(date -d "+60 days" +%F)
SX=$(guest SX); SY=$(guest SY)

# addline-with-slots: $1=student $2=slotsJSON → full post çıktısı
addslots(){ post "/api/studio/students/$SX/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"TRY\",\"preferredSlots\":$1}" "$ATOK"; }
slotcount(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.preferredSlots.length"; }

echo "--- 1) Yeni line slotsuz → 400; ≥1 slot → 200 + DTO.preferredSlots döner ---"
expect "slotsuz line → 400" 400 "$(pcode "/api/studio/students/$SX/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"TRY\"}" "$ATOK")"
expect "boş preferredSlots → 400" 400 "$(pcode "/api/studio/students/$SX/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"TRY\",\"preferredSlots\":[]}" "$ATOK")"
RL=$(addslots '[{"dayOfWeek":"Monday","preferredStartTime":"18:00:00"},{"dayOfWeek":"Thursday","preferredStartTime":null}]')
expect "≥1 slot ile line → 200" 200 "$(code "$RL")"
L1=$(body "$RL" | jget id)
expectn "L1 preferredSlots = 2" 2 "$(slotcount "$SX" "$L1")"
expect "L1 Pazartesi 18:00 slotu var" yes "$(has "$(body "$RL")" '"preferredStartTime":"18:00:00"')"
# Saat NULL → JSON'da omit edilir (WhenWritingNull); slotu parse ederek doğrula.
expectn "L1 saatsiz slot (Perşembe, time yok)" 1 "$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$L1')?.preferredSlots.filter(s=>s.dayOfWeek==='Thursday'&&s.preferredStartTime==null).length")"

echo "--- 2) Line oluştururken iç-duplicate (aynı gün+saat) → 400 ---"
expect "iç-duplicate slot → 400" 400 "$(pcode "/api/studio/students/$SX/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"18:00:00\"},{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"18:00:00\"}]}" "$ATOK")"

echo "--- 3) Slot ekle/sil + duplicate (saatli & saatsiz) ---"
expect "yeni slot ekle (Cmt saatsiz) → 200" 200 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Saturday\",\"preferredStartTime\":null}" "$ATOK")"
expectn "L1 slot sayısı 3" 3 "$(slotcount "$SX" "$L1")"
expect "aynı (Pzt,18:00) tekrar ekle → 409" 409 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"18:00:00\"}" "$ATOK")"
expect "aynı (Cmt,saatsiz) tekrar ekle → 409" 409 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Saturday\",\"preferredStartTime\":null}" "$ATOK")"
expect "aynı gün farklı saat (Pzt 20:00) → 200" 200 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":\"20:00:00\"}" "$ATOK")"
expectn "L1 slot sayısı 4" 4 "$(slotcount "$SX" "$L1")"
# bir slotu sil
SLID=$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$L1')?.preferredSlots.find(s=>s.dayOfWeek==='Saturday')?.id")
expect "slot sil (Cmt) → 200" 200 "$(dcode "/api/studio/contract-lines/preferred-slots/$SLID" "$ATOK")"
expectn "L1 slot sayısı 3 (silindi)" 3 "$(slotcount "$SX" "$L1")"
expect "silinen (Cmt,saatsiz) tekrar eklenebilir → 200" 200 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Saturday\",\"preferredStartTime\":null}" "$ATOK")"

echo "--- 4) Yetki: Instructor/Student slot yazamaz; Owner yazar ---"
expect "Instructor slot ekle → 403" 403 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Friday\",\"preferredStartTime\":null}" "$I1TOK")"
post "/api/studio/students/$SX/convert-to-user" "{\"email\":\"sx29a+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
STOK=$(login "sx29a+$TS@pf.local")
expect "Student slot ekle → 403" 403 "$(pcode "/api/studio/contract-lines/$L1/preferred-slots" "{\"dayOfWeek\":\"Friday\",\"preferredStartTime\":null}" "$STOK")"
expect "Student slot sil → 403" 403 "$(dcode "/api/studio/contract-lines/preferred-slots/$SLID" "$STOK")"

echo "--- 5) Strict payment: lineId'siz → 400; lineId ile → 200 ---"
expect "lineId'siz ödeme → 400 (strict)" 400 "$(pcode "/api/studio/students/$SX/payments" "{\"amount\":300,\"dueDate\":\"$TODAY\"}" "$ATOK")"
expect "contractId-only ödeme → 400 (strict)" 400 "$(pcode "/api/studio/students/$SX/payments" "{\"amount\":300,\"dueDate\":\"$TODAY\",\"studentContractId\":\"$(body "$RL" | jget studentContractId)\"}" "$ATOK")"
P1=$(post "/api/studio/students/$SX/payments" "{\"amount\":300,\"dueDate\":\"$TODAY\",\"studentContractLineId\":\"$L1\"}" "$ATOK")
expect "lineId ile ödeme → 200" 200 "$(code "$P1")"
PID=$(body "$P1" | jget id); pcode "/api/studio/payments/$PID/mark-paid" '{}' "$ATOK" >/dev/null
expectn "L1 paid=300" 300 "$(gbody "/api/studio/students/$SX/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$L1')?.paid")"

echo "--- 6) Legacy etiketsiz ödeme (SQL) borca SAYILMAZ; tek-line header contractId-only fallback SAYILIR ---"
# Ayrı tek-line öğrenci SY ile legacy fallback testi (SY'nin tek line'lı header'ı olur).
RYbody=$(post "/api/studio/students/$SY/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"isIndividual\":false,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Tuesday\",\"preferredStartTime\":null}]}" "$ATOK")
LY=$(body "$RYbody" | jget id); HY=$(body "$RYbody" | jget studentContractId)
# Legacy (pre-29A) tek-line header contractId-only Paid ödeme → fallback ile line.paid sayılır.
sql "INSERT INTO \"Payments\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"StudentContractId\",\"Amount\",\"Currency\",\"DueDate\",\"Status\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\") SELECT gen_random_uuid(), \"StudioId\", \"Id\", '$HY', 200, 'TRY', CURRENT_DATE, 'Paid', '00000000-0000-0000-0000-000000000000', now(), false FROM \"StudentProfiles\" WHERE \"Id\"='$SY';" >/dev/null
expectn "SY tek-line header → legacy contractId-only fallback paid=200" 200 "$(gbody "/api/studio/students/$SY/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$LY')?.paid")"
# Fully unassigned (line+contract NULL) Paid ödeme → borca SAYILMAZ, unassigned'da görünür.
sql "INSERT INTO \"Payments\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"Amount\",\"Currency\",\"DueDate\",\"Status\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\") SELECT gen_random_uuid(), \"StudioId\", \"Id\", 99, 'TRY', CURRENT_DATE, 'Paid', '00000000-0000-0000-0000-000000000000', now(), false FROM \"StudentProfiles\" WHERE \"Id\"='$SY';" >/dev/null
DEBT=$(gbody "/api/studio/students/$SY/debt" "$ATOK")
expectn "SY line paid hâlâ 200 (etiketsiz 99 sayılmaz)" 200 "$(echo "$DEBT" | jq2 "d.data.byCurrency.find(c=>c.currency==='TRY')?.paid")"
expectn "SY unassigned TRY=99" 99 "$(echo "$DEBT" | jq2 "d.data.unassignedByCurrency.find(c=>c.currency==='TRY')?.paid")"

echo "--- 7) Legacy /contracts (opsiyonel slot) → slotsuz line üretebilir (geçiş); bozulmaz ---"
SZ=$(guest SZ)
CZ=$(body "$(post "/api/studio/students/$SZ/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"totalLessonCount\":4,\"makeupAllowance\":1,\"price\":400,\"isIndividualDefault\":false}" "$ATOK")" | jget id)
LZ=$(gbody "/api/studio/students/$SZ/contract-lines" "$ATOK" | jq2 "d.data[0]?.id")
expectn "legacy /contracts line slotsuz (preferredSlots=0)" 0 "$(slotcount "$SZ" "$LZ")"
expect "slotsuz legacy line okunur (line listesi 200)" 200 "$(gcode "/api/studio/students/$SZ/contract-lines" "$ATOK")"
expect "slotsuz legacy line'a sonradan slot eklenebilir → 200" 200 "$(pcode "/api/studio/contract-lines/$LZ/preferred-slots" "{\"dayOfWeek\":\"Wednesday\",\"preferredStartTime\":\"09:00:00\"}" "$ATOK")"

echo "--- 8) /contracts opsiyonel slot ile de oluşturulabilir ---"
SW=$(guest SW)
expect "/contracts preferredSlots ile → 200" 200 "$(pcode "/api/studio/students/$SW/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"totalLessonCount\":4,\"makeupAllowance\":1,\"price\":400,\"isIndividualDefault\":false,\"preferredSlots\":[{\"dayOfWeek\":\"Friday\",\"preferredStartTime\":\"19:00:00\"}]}" "$ATOK")"
LW=$(gbody "/api/studio/students/$SW/contract-lines" "$ATOK" | jq2 "d.data[0]?.id")
expectn "/contracts line preferredSlots=1" 1 "$(slotcount "$SW" "$LW")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 29A SMOKE: TÜM TESTLER GEÇTİ ✅"; exit 0; } || { echo "SPRINT 29A SMOKE: BAŞARISIZ ❌"; exit 1; }
