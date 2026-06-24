#!/usr/bin/env bash
# Pliaforma — Sprint 32B smoke (Equipment Capacity Guard):
#  A. Basic guard: total 4, Reformer bound. 08:30-09:30 cap3 PASS; aynı saat cap1 PASS; cap1 daha FAIL.
#  B. Partial overlap: 13:00-14:00 cap2 PASS; 13:30-14:30 cap2 PASS; 13:30-14:30 cap3 FAIL.
#  C. Boundary: 15:00-16:00 cap4 PASS; 16:00-17:00 cap4 PASS (çakışma yok).
#  D. capacity > total: total 4 iken cap5 FAIL.
#  E. EquipmentId null eski davranış: ekipmansız ClassType cap9999/limitsiz PASS.
#  F. Equipment-bound + 9999: create cap9999 FAIL; update cap9999 FAIL; bulk cap default(9999) FAIL.
#  G. Legacy bind: future Scheduled cap9999 varken EquipmentId bağla → 409; sonlu yapınca bağla → 200.
#  H. Update/reschedule: kendini hariç tut; cap artırma yetmezse FAIL; dolu aralığa taşı FAIL; boş aralığa taşı PASS.
#  I. Inactive/Quantity0: pasif ekipman ile create FAIL; Quantity0 (total0) ile create FAIL.
#  J. Bulk: aralıkta üretilen önceki slotlar sonrakileri etkiler; kapasite dolunca slot atlanır (over-allocation yok).
# NOT: doğrulama hataları 400; iş-kuralı/çakışma 409. Postgres + node gerekli. ASCII-only JSON.
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
ucode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }

# Yarın (gelecek) sabit bir tarih kullan → "geçmiş zaman" engeline takılma.
D=$(date -d "+2 days" +%F)
mksess(){ post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"${D}T$3:00\",\"capacity\":$4}" "$ATOK"; }
mkcode(){ pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"${D}T$3:00\",\"capacity\":$4}" "$ATOK"; }
mkcode9999(){ pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"${D}T$3:00\"}" "$ATOK"; }

echo "== Sprint 32B: Equipment Capacity Guard =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S32B $TS\",\"ownerFullName\":\"O\",\"email\":\"o32b+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
# iki hoca (aynı saatte farklı hoca ders açabilmeli)
mkinst(){ e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
IA=$(mkinst hocaA); IB=$(mkinst hocaB)
# Equipment: Reformer total 4 (Quantity 4 x perUnit 1)
EQ=$(body "$(post /api/studio/equipment "{\"name\":\"Reformer\",\"quantity\":4,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
# Reformer-bound ClassType (60 dk)
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQ\"}" "$ATOK")" | jget id)
# Ekipmansız ClassType (E testi)
CTN=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)

echo "--- A) Basic guard (total 4) ---"
expect "08:30 cap3 (A hoca) PASS" 200 "$(mkcode "$IA" "$CT" "08:30" 3)"
expect "08:30 cap1 (B hoca) PASS" 200 "$(mkcode "$IB" "$CT" "08:30" 1)"
expect "08:30 cap1 daha (B hoca) FAIL (toplam 5)" 409 "$(mkcode "$IB" "$CT" "08:30" 1)"

echo "--- B) Partial overlap ---"
expect "13:00-14:00 cap2 PASS" 200 "$(mkcode "$IA" "$CT" "13:00" 2)"
expect "13:30-14:30 cap2 PASS (toplam 4)" 200 "$(mkcode "$IB" "$CT" "13:30" 2)"
expect "13:30-14:30 cap3 FAIL (toplam 5)" 409 "$(mkcode "$IA" "$CT" "13:35" 3)"

echo "--- C) Boundary (çakışma yok) ---"
expect "15:00-16:00 cap4 PASS" 200 "$(mkcode "$IA" "$CT" "15:00" 4)"
expect "16:00-17:00 cap4 PASS (boundary, çakışmaz)" 200 "$(mkcode "$IA" "$CT" "16:00" 4)"

echo "--- D) capacity > total ---"
expect "cap5 > total4 FAIL" 409 "$(mkcode "$IA" "$CT" "19:00" 5)"

echo "--- E) EquipmentId null eski davranış (boş hoca/saat; ekipman kısıtı YOK) ---"
expect "ekipmansız ClassType cap9999/limitsiz PASS" 200 "$(mkcode9999 "$IB" "$CTN" "01:30")"
expect "ekipmansız ClassType cap50 PASS" 200 "$(mkcode "$IB" "$CTN" "20:00" 50)"

echo "--- F) Equipment-bound + 9999 yasak ---"
expect "equipment-bound create cap9999 FAIL" 409 "$(mkcode9999 "$IA" "$CT" "21:00")"
SF=$(body "$(mksess "$IA" "$CT" "07:00" 2)" | jget id)
expect "equipment-bound update cap9999 FAIL" 409 "$(ucode "/api/studio/class-sessions/$SF" "{\"capacity\":9999}" "$ATOK")"
# bulk: kapasite verilmezse 9999 → FAIL
expect "equipment-bound bulk (cap yok=9999) FAIL" 409 "$(pcode /api/studio/class-sessions/bulk-generate "{\"instructorProfileId\":\"$IB\",\"classTypeId\":\"$CT\",\"date\":\"$D\",\"rangeStart\":\"22:00:00\",\"rangeEnd\":\"23:00:00\"}" "$ATOK")"

echo "--- G) Legacy bind kontrolü (A seçeneği) ---"
# Yeni ekipmansız ClassType'a gelecekte cap9999 session aç, sonra ekipman bağlamayı dene → 409
CTG=$(body "$(post /api/class-types "{\"name\":\"MatG\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
SG=$(body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$IA\",\"classTypeId\":\"$CTG\",\"startAt\":\"${D}T05:00:00\"}" "$ATOK")" | jget id)
EQG=$(body "$(post /api/studio/equipment "{\"name\":\"MatPool\",\"quantity\":10,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
expect "future cap9999 varken ekipman bağla FAIL 409" 409 "$(ucode "/api/class-types/$CTG" "{\"name\":\"MatG\",\"defaultDurationMinutes\":60,\"description\":null,\"isActive\":true,\"equipmentId\":\"$EQG\"}" "$ATOK")"
# o sessiona sonlu kapasite ata → sonra bağlama PASS
ucode "/api/studio/class-sessions/$SG" "{\"capacity\":5}" "$ATOK" >/dev/null
expect "sonlu kapasite sonrası ekipman bağla PASS" 200 "$(ucode "/api/class-types/$CTG" "{\"name\":\"MatG\",\"defaultDurationMinutes\":60,\"description\":null,\"isActive\":true,\"equipmentId\":\"$EQG\"}" "$ATOK")"

echo "--- H) Update/reschedule (kendini hariç) ---"
# total 4 havuz, boş bir gün-saatte session aç cap2, sonra cap4'e çıkar (kendini hariç → available 4) PASS
SH=$(body "$(mksess "$IA" "$CT" "03:00" 2)" | jget id)
expect "kendi cap 2->4 artır PASS (kendini hariç)" 200 "$(ucode "/api/studio/class-sessions/$SH" "{\"capacity\":4}" "$ATOK")"
# Başka session aynı saatte cap1 → toplam 5 olur → FAIL
expect "dolu aralıkta yeni cap1 FAIL" 409 "$(mkcode "$IB" "$CT" "03:00" 1)"
# SH'yi cap5'e çıkar → total aşımı FAIL
expect "kendi cap 4->5 FAIL (>total)" 409 "$(ucode "/api/studio/class-sessions/$SH" "{\"capacity\":5}" "$ATOK")"
# reschedule: SH'yi dolu 08:30 aralığına taşı (orada 4 dolu) → FAIL
expect "dolu 08:30 aralığına reschedule FAIL" 409 "$(pcode "/api/studio/class-sessions/$SH/reschedule" "{\"newStartAt\":\"${D}T08:30:00\",\"keepBookings\":false}" "$ATOK")"
# boş aralığa taşı (02:00) → PASS
expect "boş 02:00 aralığına reschedule PASS" 200 "$(pcode "/api/studio/class-sessions/$SH/reschedule" "{\"newStartAt\":\"${D}T02:00:00\",\"keepBookings\":false}" "$ATOK")"

echo "--- I) Inactive / Quantity 0 ---"
EQI=$(body "$(post /api/studio/equipment "{\"name\":\"PasifPool\",\"quantity\":4,\"perUnitConcurrentUsage\":1,\"isActive\":false}" "$ATOK")" | jget id)
CTI=$(body "$(post /api/class-types "{\"name\":\"Pasif\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQI\"}" "$ATOK")" | jget id)
expect "pasif ekipman ile create FAIL" 409 "$(mkcode "$IA" "$CTI" "06:00" 1)"
EQ0=$(body "$(post /api/studio/equipment "{\"name\":\"Sifir\",\"quantity\":0,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
CT0=$(body "$(post /api/class-types "{\"name\":\"Sifir\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQ0\"}" "$ATOK")" | jget id)
expect "Quantity0 (total0) ile cap1 create FAIL" 409 "$(mkcode "$IA" "$CT0" "06:00" 1)"

echo "--- J) Bulk over-allocation guard (in-batch) ---"
# total 4 havuz, boş bir günde 04:00-05:00 ve 04:00 örtüşmesi? Bulk tek hoca ardışık üretir (çakışmaz).
# Bunun yerine: önceki testlerden bağımsız boş bir ekipman + classType + cap ile bulk; tüm slotlar farklı saatte → hepsi PASS.
# In-batch over-allocation: aynı havuzu kullanan İKİNCİ classType ile aynı saatlere bulk → kapasite paylaşımı.
EQJ=$(body "$(post /api/studio/equipment "{\"name\":\"PoolJ\",\"quantity\":2,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
CTJ=$(body "$(post /api/class-types "{\"name\":\"PoolJ-CT\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQJ\"}" "$ATOK")" | jget id)
# Hoca IA, 10:00-13:00, 60dk, cap2 → 3 slot (10,11,12), her biri total2 içinde → hepsi PASS (çakışmaz)
RJ=$(body "$(post /api/studio/class-sessions/bulk-generate "{\"instructorProfileId\":\"$IA\",\"classTypeId\":\"$CTJ\",\"date\":\"$D\",\"rangeStart\":\"10:00:00\",\"rangeEnd\":\"13:00:00\",\"capacity\":2}" "$ATOK")")
expectn "bulk: 3 slot oluştu (çakışmasız)" 3 "$(echo "$RJ" | jq2 'd.data.created.length')"
# Şimdi IB ile AYNI saatlere (10:00-13:00) ikinci bulk cap1 → her saat zaten 2 dolu → hepsi atlanmalı (equipmentCapacity)
RJ2=$(body "$(post /api/studio/class-sessions/bulk-generate "{\"instructorProfileId\":\"$IB\",\"classTypeId\":\"$CTJ\",\"date\":\"$D\",\"rangeStart\":\"10:00:00\",\"rangeEnd\":\"13:00:00\",\"capacity\":1}" "$ATOK")")
expectn "bulk2: 0 slot oluştu (havuz dolu)" 0 "$(echo "$RJ2" | jq2 'd.data.created.length')"
expectn "bulk2: 3 slot equipmentCapacity ile atlandı" 3 "$(echo "$RJ2" | jq2 "d.data.skipped.filter(s=>s.reason==='equipmentCapacity').length")"

echo "--- K) Makeup approval da equipment guard'dan geçer (blocker fix) ---"
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
addline(){ post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$3\",\"startDate\":\"$(date +%F)\",\"endDate\":\"$(date -d '+90 days' +%F)\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":3,\"preferredMaxCapacity\":1,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]}" "$ATOK"; }
grant(){ pcode "/api/studio/students/$1/makeup-credits/grant" "{\"studentContractLineId\":\"$2\",\"amount\":$3,\"notes\":\"smoke\"}" "$ATOK" >/dev/null; }
mkreq(){ body "$(post /api/studio/makeup-requests "{\"studentContractLineId\":\"$1\",\"startAt\":\"${D}T$2:00\"}" "$ATOK")" | jget id; }
appr(){ pcode "/api/studio/makeup-requests/$1/approve" '{}' "$ATOK"; }
# K hocaları (önceki bölümlerle çakışmasın) + total-1 ekipman havuzu, ona bağlı CTM (equipment-bound)
ICc=$(mkinst hocaC); IDd=$(mkinst hocaD)
EQM=$(body "$(post /api/studio/equipment "{\"name\":\"SoloM\",\"quantity\":1,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
CTM=$(body "$(post /api/class-types "{\"name\":\"ReformerM\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQM\"}" "$ATOK")" | jget id)
# ekipmansız ClassType (E benzeri eski davranış için)
CTMN=$(body "$(post /api/class-types "{\"name\":\"MatM\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
SM=$(guest SM)
LM=$(body "$(addline "$SM" "$CTM" "$ICc")" | jget id)
grant "$SM" "$LM" 2   # aylık manuel kredi limiti 2 → REQ1(dolu)+REQ2(boş) için tam yeter
# Dolu test: IDd ile CTM cap1 normal session @11:00 → havuz (total1) dolar. Sonra ICc line'ı için makeup @11:00 → 409 ekipman.
expect "blocker: CTM cap1 normal @11:00 (IDd) PASS" 200 "$(mkcode "$IDd" "$CTM" "11:00" 1)"
REQ1=$(mkreq "$LM" "11:00")
expect "makeup approve dolu @11:00 → 409 (ekipman yetersiz)" 409 "$(appr "$REQ1")"
# Boş test: makeup @13:00 (boş) → 200 + resulting session Capacity=1
REQ2=$(mkreq "$LM" "14:30")
RAP=$(post "/api/studio/makeup-requests/$REQ2/approve" '{}' "$ATOK")
expect "makeup approve boş @14:30 → 200" 200 "$(code "$RAP")"
SID=$(body "$RAP" | jq2 'd.data.resultingClassSessionId')
expectn "makeup session Capacity=1 (equipment-bound)" 1 "$(gbody "/api/studio/class-sessions/$SID" "$ATOK" | jq2 'd.data.capacity')"
# EquipmentId null ClassType → eski makeup davranışı bozulmaz
SMN=$(guest SMN); LMN=$(body "$(addline "$SMN" "$CTMN" "$ICc")" | jget id); grant "$SMN" "$LMN" 2
REQ3=$(mkreq "$LMN" "16:30")
expect "ekipmansız makeup approve → 200 (eski davranış)" 200 "$(appr "$REQ3")"

echo "--- L) Sözleşme programı üretimi de equipment guard'dan geçer (ContractScheduleEngine) ---"
TODAY=$(date +%F); ENDL=$(date -d "+30 days" +%F)
# addlineX: $1=student $2=classType $3=instructor $4=isIndividual(true/false→Sprint33 preferredMaxCapacity 1/null). 30 günlük kısa aralık.
addlineX(){ local cap; [ "$4" = "true" ] && cap=1 || cap=null; post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$3\",\"startDate\":\"$TODAY\",\"endDate\":\"$ENDL\",\"lessonCount\":8,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":$cap,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]}" "$ATOK"; }
# genAt: $1=lineId $2=saat (HH:MM) → Pazartesi $2 slotuyla üret. Her alt-test FARKLI hoca+saat → instructor çakışması maskelemez.
genAt(){ pcode "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"$2:00\"}],\"reuseExistingGroup\":false}" "$ATOK"; }
genAtB(){ post "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"$2:00\"}],\"reuseExistingGroup\":false}" "$ATOK"; }
EQL=$(body "$(post /api/studio/equipment "{\"name\":\"PoolL\",\"quantity\":1,\"perUnitConcurrentUsage\":1}" "$ATOK")" | jget id)
CTL=$(body "$(post /api/class-types "{\"name\":\"ReformerL\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQL\"}" "$ATOK")" | jget id)
CTLN=$(body "$(post /api/class-types "{\"name\":\"MatL\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
CTLG=$(body "$(post /api/class-types "{\"name\":\"GrupL\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQL\"}" "$ATOK")" | jget id)
IEa=$(mkinst hocaE); IFa=$(mkinst hocaF); IGa=$(mkinst hocaG); IHa=$(mkinst hocaH)
# (1) Ekipmansız ClassType → eski davranış pass (IEa, Pzt 10:00)
S1=$(guest L1); L1L=$(body "$(addlineX "$S1" "$CTLN" "$IEa" true)" | jget id)
expect "(1) ekipmansız line generate → 200 (eski davranış)" 200 "$(genAt "$L1L" "10:00")"
# (2) Ekipman-bağlı BİREYSEL → Capacity=1 + guard pass (IFa, Pzt 11:00 — boş havuz)
S2=$(guest L2); L2L=$(body "$(addlineX "$S2" "$CTL" "$IFa" true)" | jget id)
RG2=$(genAtB "$L2L" "11:00")
expect "(2) ekipman-bağlı bireysel generate → 200" 200 "$(code "$RG2")"
expectn "(2) üretilen session Capacity=1" 1 "$(gbody "/api/studio/class-sessions?from=${TODAY}T00:00:00&to=$(date -d '+29 days' +%F)T00:00:00" "$ATOK" | jq2 "d.data.find(s=>s.classTypeId==='$CTL')?.capacity")"
# (3) Ekipman-bağlı bireysel, havuz DOLU (IFa Pzt 11:00'ı doldurdu) → IGa farklı hoca, aynı Pzt 11:00 → 409 (ekipman)
S3=$(guest L3); L3L=$(body "$(addlineX "$S3" "$CTL" "$IGa" true)" | jget id)
expect "(3) ekipman-bağlı bireysel, havuz dolu → 409" 409 "$(genAt "$L3L" "11:00")"
# (4) Ekipman-bağlı GRUP → 409 (sınırsız üretilemez) (IHa, Pzt 12:00)
S4=$(guest L4); L4L=$(body "$(addlineX "$S4" "$CTLG" "$IHa" false)" | jget id)
expect "(4) ekipman-bağlı grup generate → 409" 409 "$(genAt "$L4L" "12:00")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 32B SMOKE: TUM TESTLER GECTI ✅"; exit 0; } || { echo "SPRINT 32B SMOKE: BASARISIZ ❌"; exit 1; }
