#!/usr/bin/env bash
# Pliaforma — Sprint 32A smoke (Equipment Inventory + ClassType binding; NO enforcement):
#  - Equipment CRUD: create/list/get/update/delete.
#  - TotalCapacity = Quantity * PerUnitConcurrentUsage (4x1=4, 2x2=4).
#  - Validation: PerUnitConcurrentUsage 0/negatif → 400; Quantity negatif → 400.
#  - ClassType bind/unbind: equipmentId ata → DTO.equipmentName döner; unbind (null) → equipmentName null.
#  - Bağlı ClassType listesi (equipment detail) + boundClassTypeCount doğru.
#  - Delete Restrict: bağlı ClassType varsa equipment silinemez → 409; bağ kaldırılınca silinir → 200.
#  - Tenant isolation: başka stüdyonun owner'ı bu ekipmanı göremez/güncelleyemez → 404.
#  - Yetki: Instructor get/create → 403 (yalnız Owner/StudioAdmin).
#  - No enforcement: equipmentId=null ClassType eski davranışta (equipmentName null), CRUD bozulmaz.
# NOT: Bu yığında tüm doğrulama hataları 400 döner (422 yok). Postgres + node gerekli.
# Kullanım: scripts/sprint32a-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putc(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
ucode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
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

echo "== Sprint 32A: Equipment Inventory + ClassType binding =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S32A $TS\",\"ownerFullName\":\"O\",\"email\":\"o32a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)

echo "--- 1) Equipment CRUD + TotalCapacity ---"
R=$(post /api/studio/equipment "{\"name\":\"Reformer\",\"description\":\"studio reformer pool\",\"quantity\":4,\"perUnitConcurrentUsage\":1,\"isActive\":true}" "$ATOK")
expect "ekipman oluştur → 200" 200 "$(code "$R")"
EQ=$(body "$R" | jget id)
expectn "Reformer totalCapacity = 4 (4x1)" 4 "$(body "$R" | jq2 'd.data.totalCapacity')"
R2=$(post /api/studio/equipment "{\"name\":\"Ozel\",\"quantity\":2,\"perUnitConcurrentUsage\":2}" "$ATOK")
expectn "Özel totalCapacity = 4 (2x2)" 4 "$(body "$R2" | jq2 'd.data.totalCapacity')"
EQ2=$(body "$R2" | jget id)
expect "ekipman listesi → 200" 200 "$(gcode /api/studio/equipment "$ATOK")"
expectn "liste 2 ekipman" 2 "$(gbody /api/studio/equipment "$ATOK" | jq2 'd.data.length')"
expect "ekipman detay → 200" 200 "$(gcode "/api/studio/equipment/$EQ" "$ATOK")"
expectn "Reformer boundClassTypeCount = 0 (henüz bağ yok)" 0 "$(gbody /api/studio/equipment "$ATOK" | jq2 "d.data.find(e=>e.id==='$EQ')?.boundClassTypeCount")"
# update: quantity 4→6
expect "ekipman güncelle (adet 6) → 200" 200 "$(ucode "/api/studio/equipment/$EQ" "{\"name\":\"Reformer\",\"description\":null,\"quantity\":6,\"perUnitConcurrentUsage\":1,\"isActive\":true}" "$ATOK")"
expectn "güncel totalCapacity = 6" 6 "$(gbody "/api/studio/equipment/$EQ" "$ATOK" | jq2 'd.data.totalCapacity')"

echo "--- 2) Validation: quantity>=0, perUnitConcurrentUsage>=1 ---"
expect "quantity=0 → 200 (izinli, kapasite 0)" 200 "$(pcode /api/studio/equipment "{\"name\":\"Bozuk\",\"quantity\":0,\"perUnitConcurrentUsage\":1}" "$ATOK")"
expect "quantity negatif → 400" 400 "$(pcode /api/studio/equipment "{\"name\":\"X\",\"quantity\":-1,\"perUnitConcurrentUsage\":1}" "$ATOK")"
expect "perUnitConcurrentUsage 0 → 400" 400 "$(pcode /api/studio/equipment "{\"name\":\"X\",\"quantity\":2,\"perUnitConcurrentUsage\":0}" "$ATOK")"
expect "perUnitConcurrentUsage negatif → 400" 400 "$(pcode /api/studio/equipment "{\"name\":\"X\",\"quantity\":2,\"perUnitConcurrentUsage\":-3}" "$ATOK")"
expect "ad boş → 400" 400 "$(pcode /api/studio/equipment "{\"name\":\"\",\"quantity\":2,\"perUnitConcurrentUsage\":1}" "$ATOK")"

echo "--- 3) ClassType bind/unbind + equipmentName ---"
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer Baslangic\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQ\"}" "$ATOK")" | jget id)
expect "ekipmana bağlı ClassType → equipmentName=Reformer" Reformer "$(gbody /api/class-types "$ATOK" | jq2 "d.data.find(c=>c.id==='$CT')?.equipmentName")"
expect "ClassType.equipmentId = EQ" "$EQ" "$(gbody /api/class-types "$ATOK" | jq2 "d.data.find(c=>c.id==='$CT')?.equipmentId")"
# ikinci ClassType da aynı havuza (N:1 paylaşım)
CT2=$(body "$(post /api/class-types "{\"name\":\"Reformer Ileri\",\"defaultDurationMinutes\":60,\"equipmentId\":\"$EQ\"}" "$ATOK")" | jget id)
expectn "Reformer boundClassTypeCount = 2 (paylaşım)" 2 "$(gbody /api/studio/equipment "$ATOK" | jq2 "d.data.find(e=>e.id==='$EQ')?.boundClassTypeCount")"
expectn "equipment detail boundClassTypes uzunluk 2" 2 "$(gbody "/api/studio/equipment/$EQ" "$ATOK" | jq2 'd.data.boundClassTypes.length')"
expect "boundClassTypes 'Reformer Baslangic' içerir" yes "$(has "$(gbody "/api/studio/equipment/$EQ" "$ATOK")" 'Reformer Baslangic')"
# unbind: CT2'yi ekipmandan çöz
expect "ClassType unbind (equipmentId null) → 200" 200 "$(ucode "/api/class-types/$CT2" "{\"name\":\"Reformer Ileri\",\"defaultDurationMinutes\":60,\"description\":null,\"isActive\":true,\"equipmentId\":null}" "$ATOK")"
expect "unbind sonrası equipmentName null" "" "$(gbody /api/class-types "$ATOK" | jq2 "d.data.find(c=>c.id==='$CT2')?.equipmentName")"
expectn "Reformer boundClassTypeCount = 1 (unbind sonrası)" 1 "$(gbody /api/studio/equipment "$ATOK" | jq2 "d.data.find(e=>e.id==='$EQ')?.boundClassTypeCount")"
# olmayan/başka stüdyo ekipmanına bağlama → 404
expect "geçersiz equipmentId ile ClassType → 404" 404 "$(pcode /api/class-types "{\"name\":\"Y\",\"defaultDurationMinutes\":60,\"equipmentId\":\"00000000-0000-0000-0000-000000000099\"}" "$ATOK")"

echo "--- 4) Delete Restrict: bağlı ekipman silinemez (409); bağ kalkınca silinir ---"
expect "bağlı ekipman sil → 409" 409 "$(dcode "/api/studio/equipment/$EQ" "$ATOK")"
# CT'yi de unbind et → bağ kalmasın
ucode "/api/class-types/$CT" "{\"name\":\"Reformer Baslangic\",\"defaultDurationMinutes\":60,\"description\":null,\"isActive\":true,\"equipmentId\":null}" "$ATOK" >/dev/null
expect "bağ kalmayınca ekipman sil → 200" 200 "$(dcode "/api/studio/equipment/$EQ" "$ATOK")"
expectn "silinen ekipman listede yok" 0 "$(gbody /api/studio/equipment "$ATOK" | jq2 "d.data.filter(e=>e.id==='$EQ').length")"

echo "--- 5) No enforcement: equipmentId=null ClassType eski davranışta ---"
CT3=$(body "$(post /api/class-types "{\"name\":\"Mat (ekipmansiz)\",\"defaultDurationMinutes\":50}" "$ATOK")" | jget id)
expect "ekipmansız ClassType equipmentId null" "" "$(gbody /api/class-types "$ATOK" | jq2 "d.data.find(c=>c.id==='$CT3')?.equipmentId")"
expect "ekipmansız ClassType equipmentName null" "" "$(gbody /api/class-types "$ATOK" | jq2 "d.data.find(c=>c.id==='$CT3')?.equipmentName")"

echo "--- 6) Tenant isolation: başka stüdyo bu ekipmanı göremez ---"
BTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S32A-B $TS\",\"ownerFullName\":\"OB\",\"email\":\"o32ab+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
expect "diğer stüdyo, EQ2 detay → 404" 404 "$(gcode "/api/studio/equipment/$EQ2" "$BTOK")"
expect "diğer stüdyo, EQ2 güncelle → 404" 404 "$(ucode "/api/studio/equipment/$EQ2" "{\"name\":\"Z\",\"description\":null,\"quantity\":1,\"perUnitConcurrentUsage\":1,\"isActive\":true}" "$BTOK")"
expectn "diğer stüdyo listesi EQ2'yi içermez" 0 "$(gbody /api/studio/equipment "$BTOK" | jq2 "d.data.filter(e=>e.id==='$EQ2').length")"

echo "--- 7) Yetki: Instructor ekipman göremez/oluşturamaz (403) ---"
e="i32a+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"I\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ITOK=$(login "$e")
expect "Instructor ekipman listele → 403" 403 "$(gcode /api/studio/equipment "$ITOK")"
expect "Instructor ekipman oluştur → 403" 403 "$(pcode /api/studio/equipment "{\"name\":\"Hack\",\"quantity\":1,\"perUnitConcurrentUsage\":1}" "$ITOK")"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 32A SMOKE: TÜM TESTLER GEÇTİ ✅"; exit 0; } || { echo "SPRINT 32A SMOKE: BAŞARISIZ ❌"; exit 1; }
