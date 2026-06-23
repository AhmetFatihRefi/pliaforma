#!/usr/bin/env bash
# Pliaforma — Sprint 21 smoke (Calendar Reschedule, Chain-Shift & Schedule Change UX):
#  - Tek taşıma: HOCA çakışması → 409; farklı hoca aynı saat → 200; ÖĞRENCİ çakışması → 409 (Sprint 21 gap fix).
#  - Taşıma telafi ÜRETMEZ; booking korunur; EndAt = yeni başlangıç + süre; geçmişe taşıma → 409.
#  - Chain-shift preview doğru dersleri/çakışmayı gösterir; apply zinciri kaydırır; boşlukta zincir durur;
#    zincir-dışı çakışmada ATOMİK abort (hiçbir ders kaymaz); telafi yok.  Ayrım: NotDelivered telafi VAR.
#  - Yetki: Instructor chain-shift/preview → 403 (Owner/StudioAdmin).
# Postgres container gerekli. Kullanım: scripts/sprint21-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
bal(){ sql "SELECT COALESCE(SUM(\"Amount\"),0) FROM \"MakeupCreditTransactions\" WHERE \"StudioId\"='$SID' AND \"StudentProfileId\"='$1';"; }
sstart(){ sql "SELECT to_char(\"StartAt\",'HH24:MI') FROM \"ClassSessions\" WHERE \"Id\"='$1';"; }
send(){ sql "SELECT to_char(\"EndAt\",'HH24:MI') FROM \"ClassSessions\" WHERE \"Id\"='$1';"; }
resched(){ pcode "/api/studio/class-sessions/$1/reschedule" "{\"newStartAt\":\"$2\",\"keepBookings\":${3:-true}}" "${4:-$ATOK}"; }
preview(){ curl -s "$BASE/api/studio/class-sessions/$1/chain-shift/preview?newStartAt=$2" -H "Authorization: Bearer ${3:-$ATOK}"; }
chain(){ pcode "/api/studio/class-sessions/$1/chain-shift" "{\"newStartAt\":\"$2\"}" "${3:-$ATOK}"; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S21 $TS\",\"ownerFullName\":\"O\",\"email\":\"o21+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S21 $TS';")
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
St1=$(guest St1); St2=$(guest St2); St3=$(guest St3)
mkc(){ post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":50,\"makeupAllowance\":3,\"price\":0,\"isIndividualDefault\":false}" "$ATOK" >/dev/null; }
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\"}" "$ATOK")" | jget id; }
book(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }
mkc "$St1"; mkc "$St2"; mkc "$St3"
D3=$(date -d "+3 days" +%Y-%m-%d); D4=$(date -d "+4 days" +%Y-%m-%d); D5=$(date -d "+5 days" +%Y-%m-%d)

echo "--- 1) Tek taşıma: öğrenci + hoca çakışması; farklı hoca aynı saat ---"
SA=$(mksess "$I1P" "${D3}T10:00:00"); book "$SA" "$St1" >/dev/null
SB=$(mksess "$I1P" "${D3}T12:00:00"); BB=$(book "$SB" "$St1")
expect "SB'yi 10:00'a taşı → ÖĞRENCİ çakışması 409 (St1 SA'da)" 409 "$(resched "$SB" "${D3}T10:00:00")"
expect "SB'yi 14:00'a taşı (boş) → 200" 200 "$(resched "$SB" "${D3}T14:00:00")"
expect "taşıma sonrası SB başlangıç 14:00" "14:00" "$(sstart "$SB")"
expect "EndAt = 15:00 (süreden)" "15:00" "$(send "$SB")"
expect "booking korundu (St1 SB'de hâlâ Booked)" Booked "$(sql "SELECT \"Status\" FROM \"ClassBookings\" WHERE \"Id\"='$BB';")"
expect "taşıma telafi üretmez (St1 bakiye 0)" 0 "$(bal "$St1")"
SC=$(mksess "$I1P" "${D3}T16:00:00")
expect "SC'yi 10:00'a taşı → HOCA çakışması 409 (I1P SA'da)" 409 "$(resched "$SC" "${D3}T10:00:00")"
SD=$(mksess "$I2P" "${D3}T18:00:00")
expect "farklı hoca (I2P) 10:00'a → 200 (stüdyo-geneli engel yok)" 200 "$(resched "$SD" "${D3}T10:00:00")"
expect "geçmişe taşıma → 409" 409 "$(resched "$SC" "2020-01-01T10:00:00")"

echo "--- 2) Chain-shift: preview + apply + boşlukta dur ---"
T1=$(mksess "$I1P" "${D4}T10:00:00"); T2=$(mksess "$I1P" "${D4}T11:00:00"); T3=$(mksess "$I1P" "${D4}T12:00:00"); T4=$(mksess "$I1P" "${D4}T15:00:00")
book "$T2" "$St1" >/dev/null
PV=$(preview "$T1" "${D4}T10:30:00")
expect "preview: 3 ders kayacak (T1,T2,T3; T4 boşlukta hariç)" 3 "$(echo "$PV" | grep -o '"sessionId"' | wc -l | tr -d ' ')"
expect "preview canApply true" yes "$(has "$PV" '"canApply":true')"
expect "chain-shift uygula → 200" 200 "$(chain "$T1" "${D4}T10:30:00")"
expect "T1 → 10:30" "10:30" "$(sstart "$T1")"
expect "T2 → 11:30 (zincir)" "11:30" "$(sstart "$T2")"
expect "T3 → 12:30 (zincir)" "12:30" "$(sstart "$T3")"
expect "T4 değişmedi (boşlukta zincir durdu)" "15:00" "$(sstart "$T4")"
expect "chain-shift telafi üretmez (St1 bakiye 0)" 0 "$(bal "$St1")"

echo "--- 3) Chain-shift zincir-dışı çakışma → ATOMİK abort ---"
# Sprint 28C-1: booking resolver instructor-zorunlu → St2'nin I2P seansına (U) bookluyabilmesi için I2P line'ı gerekir.
post "/api/studio/students/$St2/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I2P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":50,\"makeupAllowance\":3,\"price\":0,\"isIndividualDefault\":false}" "$ATOK" >/dev/null
C1=$(mksess "$I1P" "${D5}T10:00:00"); C2=$(mksess "$I1P" "${D5}T11:00:00")
book "$C2" "$St2" >/dev/null
U=$(mksess "$I2P" "${D5}T12:15:00"); book "$U" "$St2" >/dev/null   # St2: C2(11-12) + U(12:15-13:15) çakışmaz
PV2=$(preview "$C1" "${D5}T10:30:00")
expect "preview canApply false (C2 kayınca St2 ↔ U çakışır)" no "$(has "$PV2" '"canApply":true')"
expect "preview öğrenci çakışması işaretli" yes "$(has "$PV2" '"conflictReason":"student"')"
expect "chain-shift apply → 409 (atomik)" 409 "$(chain "$C1" "${D5}T10:30:00")"
expect "ATOMİK: C1 değişmedi" "10:00" "$(sstart "$C1")"
expect "ATOMİK: C2 değişmedi" "11:00" "$(sstart "$C2")"

echo "--- 4) Ayrım: reschedule telafi yok vs NotDelivered telafi var ---"
RD=$(mksess "$I1P" "${D3}T09:00:00"); book "$RD" "$St3" >/dev/null
resched "$RD" "${D3}T09:30:00" >/dev/null
expect "reschedule sonrası St3 bakiye 0 (telafi yok)" 0 "$(bal "$St3")"
ND=$(mksess "$I1P" "${D3}T20:00:00"); book "$ND" "$St3" >/dev/null
pcode "/api/studio/class-sessions/$ND/not-delivered" '{}' "$ATOK" >/dev/null
expect "not-delivered sonrası St3 bakiye 1 (telafi var)" 1 "$(bal "$St3")"

echo "--- 5) Yetki: chain-shift yalnız Owner/StudioAdmin ---"
expect "Instructor chain-shift → 403" 403 "$(chain "$T1" "${D4}T11:00:00" "$I2TOK")"
expect "Instructor chain-shift preview → 403" 403 "$(gcode "/api/studio/class-sessions/$T1/chain-shift/preview?newStartAt=${D4}T11:00:00" "$I2TOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 21 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 21 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
