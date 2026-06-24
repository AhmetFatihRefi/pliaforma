#!/usr/bin/env bash
# Pliaforma — Sprint 32C smoke (Availability finder / Boşluk Bul):
#  A. Basic window: total4, 16:00 Reformer cap2 → required4 dur60 → son geçerli başlangıç 15:00; 15:10 yok.
#  B. Required farkı: aynı kurulum required2 → 15:10 geçerli (cap2).
#  C. Peak (bitişik seans): total4, 14:00-14:30 cap3 + 14:30-15:00 cap3 → peak3 → required1 OK (14:00), required2 değil.
#  D. Boundary: total4, 13:00-14:00 cap4 → 14:00 çakışmaz (geçerli); 13:00 değil.
#  E. EquipmentId null: ekipmansız ClassType → availableEquipmentCapacity null, hoca bazlı.
#  F. Instructor filter: instructorId verilince sadece o hoca; o hoca meşgulse o saat yok; filtresiz uygun hocalar.
#  G. Equipment inactive / Quantity0 → equipment-bound için sonuç YOK.
#  H. Past filtering: bugün için geçmiş başlangıçlar dönmez.
#  I. Date range: çok gün + max-range (>31 gün) validation 400.
#  J. RBAC: Owner/Instructor 200, Student 403, anonim 401.
# ASCII-only JSON. Postgres + node gerekli.
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null 2>&1; }
D2=$(date -d "+2 days" +%F)
av(){ gbody "/api/studio/availability/class-type-slots?classTypeId=$1&date=$D2&durationMinutes=60&requiredCapacity=$2&incrementMinutes=10${3:+&instructorId=$3}" "${4:-$ATOK}"; }
avcode(){ gcode "/api/studio/availability/class-type-slots?classTypeId=$1&date=$D2&durationMinutes=60&requiredCapacity=1&incrementMinutes=10" "$2"; }
# pencere yardımcıları (HH:MM grid başlangıcı bir pencerede mi / kapasitesi / pencere bitişi)
hasStart(){ echo "$1" | jq2 "d.data.some(w=>w.startWindowFrom.slice(11,16)<='$2'&&'$2'<=w.startWindowTo.slice(11,16))"; }
capAt(){ echo "$1" | jq2 "(d.data.find(w=>w.startWindowFrom.slice(11,16)<='$2'&&'$2'<=w.startWindowTo.slice(11,16))||{}).availableEquipmentCapacity"; }
instrAt(){ echo "$1" | jq2 "((d.data.find(w=>w.startWindowFrom.slice(11,16)<='$2'&&'$2'<=w.startWindowTo.slice(11,16))||{}).availableInstructors||[]).map(i=>i.name).join(',')"; }

echo "== Sprint 32C: Availability finder =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S32C $TS\",\"ownerFullName\":\"O\",\"email\":\"o32c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
mkinst(){ e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$e\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
IA=$(mkinst hocaA); IB=$(mkinst hocaB); IBTOK=$(login "hocaB+$TS@pf.local")
mkeq(){ body "$(post /api/studio/equipment "{\"name\":\"$1\",\"quantity\":$2,\"perUnitConcurrentUsage\":1${3:+,\"isActive\":$3}}" "$ATOK")" | jget id; }
mkct(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":$2${3:+,\"equipmentId\":\"$3\"}}" "$ATOK")" | jget id; }
mksess(){ post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"${D2}T$3:00\",\"capacity\":$4}" "$ATOK" >/dev/null; }

echo "--- A/B) Basic window + required farkı (total4, 16:00 cap2) ---"
EQA=$(mkeq PoolA 4); CTA=$(mkct ReformerA 60 "$EQA")
mksess "$IA" "$CTA" "16:00" 2     # 16:00-17:00 cap2 (IA) → havuzdan 2
RA4=$(av "$CTA" 4 "$IB")          # required4, IB (boş hoca) ile sorgula → ekipman izole
expect "A: 15:00 başlangıç geçerli (required4)" true "$(hasStart "$RA4" "15:00")"
expect "A: 15:00 kapasitesi 4" 4 "$(capAt "$RA4" "15:00")"
expect "A: 15:10 başlangıç YOK (16:00'a taşar, avail2<4)" false "$(hasStart "$RA4" "15:10")"
RA2=$(av "$CTA" 2 "$IB")          # required2
expect "B: 15:10 başlangıç geçerli (required2)" true "$(hasStart "$RA2" "15:10")"
expect "B: 15:10 kapasitesi 2" 2 "$(capAt "$RA2" "15:10")"

echo "--- C) Peak (bitişik seans, sum değil) total4, 14:00-14:30 cap3 + 14:30-15:00 cap3 ---"
EQC=$(mkeq PoolC 4); CTC=$(mkct ReformerC 30 "$EQC")   # 30 dk → bitişik iki seans
mksess "$IA" "$CTC" "14:00" 3; mksess "$IA" "$CTC" "14:30" 3
RC1=$(av "$CTC" 1 "$IB"); RC2=$(av "$CTC" 2 "$IB")
expect "C: 14:00 required1 geçerli (peak3, avail1)" true "$(hasStart "$RC1" "14:00")"
expect "C: 14:00 kapasitesi 1 (peak; sum olsaydı negatif)" 1 "$(capAt "$RC1" "14:00")"
expect "C: 14:00 required2 GEÇERSIZ (avail1<2)" false "$(hasStart "$RC2" "14:00")"

echo "--- D) Boundary (13:00-14:00 cap4, 14:00 çakışmaz) ---"
EQD=$(mkeq PoolD 4); CTD=$(mkct ReformerD 60 "$EQD")
mksess "$IA" "$CTD" "13:00" 4
RD=$(av "$CTD" 4 "$IB")
expect "D: 14:00 başlangıç geçerli (boundary)" true "$(hasStart "$RD" "14:00")"
expect "D: 14:00 kapasitesi 4" 4 "$(capAt "$RD" "14:00")"
expect "D: 13:00 başlangıç YOK (dolu)" false "$(hasStart "$RD" "13:00")"

echo "--- E) EquipmentId null (ekipmansız) ---"
CTN=$(mkct MatN 60)
RE=$(av "$CTN" 5 "$IB")
expect "E: 12:00 başlangıç geçerli" true "$(hasStart "$RE" "12:00")"
expect "E: availableEquipmentCapacity null (ekipmansız)" "" "$(capAt "$RE" "12:00")"

echo "--- F) Instructor filter ---"
mksess "$IB" "$CTN" "10:00" 5     # IB 10:00-11:00 meşgul (ekipmansız → yalnız hoca müsaitliği)
RF_IB=$(av "$CTA" 1 "$IB")        # CTA, yalnız IB
expect "F: IB filtresiyle 10:00 YOK (IB meşgul)" false "$(hasStart "$RF_IB" "10:00")"
expect "F: IB filtresiyle 12:00 var (IB boş)" true "$(hasStart "$RF_IB" "12:00")"
RF_ALL=$(av "$CTA" 1)             # filtresiz → uygun hocalar
expect "F: filtresiz 10:00'da IA var (IB meşgul, IA boş)" "hocaA" "$(instrAt "$RF_ALL" "10:00")"

echo "--- G) Equipment inactive / Quantity0 → sonuç yok ---"
EQG=$(mkeq PoolG 4 false); CTG=$(mkct ReformerG 60 "$EQG")
expect "G: pasif ekipman → 0 pencere" 0 "$(av "$CTG" 1 "$IB" | jq2 'd.data.length')"
EQ0=$(mkeq Pool0 0); CT0=$(mkct Reformer0 60 "$EQ0")
expect "G: Quantity0 → 0 pencere" 0 "$(av "$CT0" 1 "$IB" | jq2 'd.data.length')"

echo "--- H) Past filtering (bugün) ---"
RH=$(gbody "/api/studio/availability/class-type-slots?classTypeId=$CTN&date=$(date +%F)&durationMinutes=60&requiredCapacity=1&incrementMinutes=15" "$ATOK")
expect "H: bugün için geçmiş başlangıç YOK (tüm start >= şimdi)" true "$(echo "$RH" | jq2 "d.data.every(w=>new Date(w.startWindowFrom).getTime() >= Date.now()-60000)")"

echo "--- I) Date range ---"
RI=$(gbody "/api/studio/availability/class-type-slots?classTypeId=$CTN&from=$D2&to=$(date -d '+4 days' +%F)&durationMinutes=60&requiredCapacity=1&incrementMinutes=30" "$ATOK")
expect "I: çok günlü sonuç (>=2 farklı tarih)" true "$(echo "$RI" | jq2 "new Set(d.data.map(w=>w.date)).size>=2")"
# Aralık asimi iş-kuralı (BusinessRuleException) → 409 (GetClassSessions deseniyle tutarli).
expect "I: max-range asimi (>31 gün) → 409" 409 "$(gcode "/api/studio/availability/class-type-slots?classTypeId=$CTN&from=$(date +%F)&to=$(date -d '+40 days' +%F)&durationMinutes=60&requiredCapacity=1" "$ATOK")"

echo "--- J) RBAC ---"
expect "J: Owner → 200" 200 "$(avcode "$CTN" "$ATOK")"
expect "J: Instructor → 200" 200 "$(avcode "$CTN" "$IBTOK")"
expect "J: anonim → 401" 401 "$(avcode "$CTN" "")"
# Student: bir guest'i user'a çevir
SX=$(body "$(post /api/studio/students/guest "{\"fullName\":\"SX\",\"source\":\"Phone\"}" "$ATOK")" | jget id)
post "/api/studio/students/$SX/convert-to-user" "{\"email\":\"sx32c+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
STOK=$(login "sx32c+$TS@pf.local")
expect "J: Student → 403" 403 "$(avcode "$CTN" "$STOK")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 32C SMOKE: TUM TESTLER GECTI ✅"; exit 0; } || { echo "SPRINT 32C SMOKE: BASARISIZ ❌"; exit 1; }
