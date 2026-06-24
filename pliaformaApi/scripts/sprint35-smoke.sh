#!/usr/bin/env bash
# Pliaforma — Sprint 35 smoke (Haftalık çoklu ders slotu / haftada N ders üretimi):
#  A. 8 ders / 2 slot → 4 hafta (her hafta 2 slot); 7 ders / 2 slot → 3 tam hafta + 1; 8 ders / 1 slot → 8 hafta.
#  B. Aynı gün farklı saat (Sal 10:00 + Sal 18:00) → ikisi de üretilir.
#  C. Duplicate aynı gün+saat → create/preferences 400.
#  D. Saatsiz slot üretime girmez (replan time-bearing filtreler).
#  G. Replan + çoklu slot: slot seti değişince kalan dersler yeni çoklu slot setiyle üretilir.
# Backend DEĞİŞMEDİ — bu smoke engine'in çoklu-slot haftalık davranışını doğrular. ASCII-only. Postgres+node.
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
TS=$(date +%s); TODAY=$(date +%F); END=$(date -d "+200 days" +%F)
ipid(){ gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 35: haftalık çoklu slot üretimi =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S35 $TS\",\"ownerFullName\":\"O\",\"email\":\"o35+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
mkinst(){ e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
IA=$(mkinst hocaA)
CTMAT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
S(){ echo "{\"dayOfWeek\":\"$1\",\"preferredStartTime\":\"$2\",\"notes\":null}"; }  # slot JSON
# addlineS: $1 student $2 instr("") $3 cap(null) $4 lessonCount $5 slotsJSON-array
addlineS(){ local instr="${2:+\"$2\"}"; instr="${instr:-null}"; local cap="${3:-null}"; post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CTMAT\",\"instructorProfileId\":$instr,\"startDate\":\"$TODAY\",\"endDate\":\"$END\",\"lessonCount\":$4,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":$cap,\"currency\":\"TRY\",\"preferredSlots\":$5}" "$ATOK"; }
# generate slots = preferredSlots ($2) → ContractScheduleSlotInput (preferredStartTime → startTime). Frontend de aynısını yapar.
genS(){ local gs=$(echo "$2" | sed 's/preferredStartTime/startTime/g'); pcode "/api/studio/contract-lines/$1/schedule/generate" "{\"slots\":$gs,\"reuseExistingGroup\":true${3:+,\"instructorProfileId\":\"$3\"}}" "$ATOK"; }
prefs(){ putc "/api/studio/contract-lines/$1/preferences" "$2" "$ATOK"; }
replan(){ pcode "/api/studio/contract-lines/$1/replan" "${2:-{}}" "$ATOK"; }
# psql sayaçlar (booked + future scheduled regular, line bazlı)
fut(){ sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractLineId\"='$1' AND b.\"Status\"='Booked' AND s.\"SessionKind\"='Regular' AND s.\"Status\"='Scheduled';"; }
dayN(){ sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractLineId\"='$1' AND b.\"Status\"='Booked' AND s.\"Status\"='Scheduled' AND trim(to_char(s.\"StartAt\",'Day'))='$2';"; }
wkN(){ sql "SELECT count(DISTINCT to_char(s.\"StartAt\",'IYYY-IW')) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractLineId\"='$1' AND b.\"Status\"='Booked' AND s.\"Status\"='Scheduled';"; }
timeN(){ sql "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE b.\"StudentContractLineId\"='$1' AND b.\"Status\"='Booked' AND s.\"Status\"='Scheduled' AND to_char(s.\"StartAt\",'HH24:MI')='$2';"; }

echo "--- A) Çoklu slot hafta dağıtımı ---"
# 8 ders / 2 slot (Pzt 10:00 + Per 18:00) → 4 hafta, 4 Pzt + 4 Per
SA=$(guest A35); SL2="[$(S Monday 10:00:00),$(S Thursday 18:00:00)]"; LA=$(body "$(addlineS "$SA" "$IA" "" 8 "$SL2")" | jget id)
genS "$LA" "$SL2" >/dev/null
expectn "A: 8 ders / 2 slot → toplam 8 üretildi" 8 "$(fut "$LA")"
expectn "A: 4 Pazartesi" 4 "$(dayN "$LA" Monday)"
expectn "A: 4 Perşembe" 4 "$(dayN "$LA" Thursday)"
# (ISO-hafta sayısı StartDate'in hafta içindeki konumuna göre 4–5 olabilir; gün dağılımı asıl güvence)
# 7 ders / 2 slot → 3 tam hafta + 1; toplam 7, günler 3+4 (hangi günün fazla olduğu StartDate gününe bağlı)
SB=$(guest B35); LB=$(body "$(addlineS "$SB" "$IA" "" 7 "$SL2")" | jget id); genS "$LB" "$SL2" >/dev/null
MB=$(dayN "$LB" Monday); TB=$(dayN "$LB" Thursday)
expectn "A: 7 ders / 2 slot → 7 üretildi" 7 "$(fut "$LB")"
expectn "A: 7/2 → Pzt+Per = 7" 7 "$((MB+TB))"
expectn "A: 7/2 → gün dağılımı 3+4 (çarpım 12)" 12 "$((MB*TB))"
# 8 ders / 1 slot → 8 hafta
SC=$(guest C35); SL1="[$(S Monday 10:00:00)]"; LC=$(body "$(addlineS "$SC" "$IA" "" 8 "$SL1")" | jget id); genS "$LC" "$SL1" >/dev/null
expectn "A: 8 ders / 1 slot → 8 Pazartesi" 8 "$(dayN "$LC" Monday)"
expectn "A: 8 ders / 1 slot → 8 hafta" 8 "$(wkN "$LC")"

echo "--- B) Aynı gün farklı saat ---"
SD=$(guest D35); SLT="[$(S Tuesday 10:00:00),$(S Tuesday 18:00:00)]"; LD=$(body "$(addlineS "$SD" "$IA" "" 4 "$SLT")" | jget id); genS "$LD" "$SLT" >/dev/null
expectn "B: 4 ders hepsi Salı" 4 "$(dayN "$LD" Tuesday)"
expectn "B: 2 ders 10:00" 2 "$(timeN "$LD" 10:00)"
expectn "B: 2 ders 18:00" 2 "$(timeN "$LD" 18:00)"
expectn "B: 2 farklı hafta" 2 "$(wkN "$LD")"

echo "--- C) Duplicate aynı gün+saat ---"
SE=$(guest E35); DUP="[$(S Monday 10:00:00),$(S Monday 10:00:00)]"
expect "C: create aynı gün+saat dup → 400" 400 "$(code "$(addlineS "$SE" "$IA" "" 4 "$DUP")")"
LE=$(body "$(addlineS "$SE" "$IA" "" 4 "$SL1")" | jget id)
expect "C: preferences dup → 400" 400 "$(prefs "$LE" "{\"instructorProfileId\":\"$IA\",\"preferredMaxCapacity\":null,\"preferredSlots\":[$(S Monday 10:00:00),$(S Monday 10:00:00)]}")"

echo "--- D) Saatsiz slot üretime girmez (replan) ---"
# Mon 10:00 (saatli) + Wed (saatsiz). Replan yalnız saatli slotu kullanmalı → hepsi Pazartesi.
SF=$(guest F35); MIX="[$(S Monday 10:00:00),{\"dayOfWeek\":\"Wednesday\",\"preferredStartTime\":null,\"notes\":null}]"
LF=$(body "$(addlineS "$SF" "$IA" "" 4 "$MIX")" | jget id)
replan "$LF" >/dev/null
expectn "D: replan → 4 üretildi (saatli slot)" 4 "$(fut "$LF")"
expectn "D: hepsi Pazartesi (saatsiz Çarşamba kullanılmadı)" 4 "$(dayN "$LF" Monday)"
expectn "D: Çarşamba seans YOK" 0 "$(dayN "$LF" Wednesday)"

echo "--- G) Replan + çoklu slot seti değişimi ---"
SG=$(guest G35); LG=$(body "$(addlineS "$SG" "$IA" "" 4 "$SL2")" | jget id); genS "$LG" "$SL2" >/dev/null
expectn "G: ilk üretim 4 (Pzt+Per)" 4 "$(fut "$LG")"
# slot setini Salı 11:00 + Cuma 15:00 yap, replan
prefs "$LG" "{\"instructorProfileId\":\"$IA\",\"preferredMaxCapacity\":null,\"preferredSlots\":[$(S Tuesday 11:00:00),$(S Friday 15:00:00)]}" >/dev/null
replan "$LG" >/dev/null
expectn "G: replan sonrası 4 (yeni set)" 4 "$(fut "$LG")"
expectn "G: 2 Salı (yeni slot)" 2 "$(dayN "$LG" Tuesday)"
expectn "G: 2 Cuma (yeni slot)" 2 "$(dayN "$LG" Friday)"
expectn "G: eski Pazartesi seans (Scheduled booked) kalmadı" 0 "$(dayN "$LG" Monday)"

echo
[ "$FAILED" = "0" ] && { echo "SPRINT 35 SMOKE: TUM TESTLER GECTI ✅"; exit 0; } || { echo "SPRINT 35 SMOKE: BASARISIZ ❌"; exit 1; }
