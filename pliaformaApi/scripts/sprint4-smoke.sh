#!/usr/bin/env bash
# Pliaforma — Sprint 4 smoke (rezervasyon, kapasite, çift-booking, iptal/geri-açma, paralel yarış,
# guest/inactive/self kuralları, instructor scope, tenant izolasyonu, student engeli).
# Kullanım: scripts/sprint4-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
FUT=$(date -d "+2 days" +%Y-%m-%d)

# --- Stüdyo A + C + hocalar ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S4A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s4a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
RC=$(post /api/auth/register-studio "{\"studioName\":\"S4C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s4c+$TS@pf.local\",\"password\":\"$PW\"}"); CTOK=$(body "$RC" | jget accessToken)
for n in i1 i2; do post /api/auth/register "{\"fullName\":\"$n\",\"email\":\"$n+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null; done
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
inv(){ local e="$1+$TS@pf.local"; vrf "$e"; RR=$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); M=$(body "$RR" | jget membershipId); T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; ipid "$e"; }
I1P=$(inv i1); I2P=$(inv i2); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":2,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
# Seanslar (Owner availability'siz açabilir)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"${FUT}T$2:00\",\"capacity\":$3}" "$ATOK")" | jget id; }
SESS1=$(mksess "$I1P" "10:00" 1); SESS2=$(mksess "$I1P" "11:00" 2); RACE=$(mksess "$I1P" "14:00" 1)
# Öğrenciler
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
SG=$(guest "GuestS"); SI=$(guest "InactiveS"); pcode "/api/studio/students/$SI/deactivate" '{}' "$ATOK" >/dev/null
# Aktif öğrenci (convert) + token
mkactive(){ local g; g=$(guest "$1"); post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$1+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; echo "$g"; }
SA=$(mkactive "sa"); SATOK=$(login "sa+$TS@pf.local")
SOTH=$(mkactive "sb"); SOTOK=$(login "sb+$TS@pf.local")
# Sprint 18b: Regular booking SÖZLEŞME ister (paket değil). Booked öğrencilere geniş sözleşme (auto-resolve).
mkc(){ curl -s -o /dev/null -X POST "$BASE/api/studio/students/$1/contracts" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":100,\"makeupAllowance\":5,\"price\":0,\"isIndividualDefault\":false}"; }
mkc "$SA"; mkc "$SG"

echo "--- Kapasite & çift booking ---"
R=$(post "/api/studio/class-sessions/$SESS1/bookings" "{\"studentProfileId\":\"$SA\"}" "$ATOK")
expect "ilk booking (cap=1)" 200 "$(code "$R")"; BK1=$(body "$R" | jget id)
expect "dolu seansa 2. booking → 409" 409 "$(pcode "/api/studio/class-sessions/$SESS1/bookings" "{\"studentProfileId\":\"$SG\"}" "$ATOK")"
expect "booking iptal" 200 "$(pcode "/api/studio/bookings/$BK1/cancel" '{}' "$ATOK")"
expect "iptal sonrası kapasite açıldı (yeni booking)" 200 "$(pcode "/api/studio/class-sessions/$SESS1/bookings" "{\"studentProfileId\":\"$SG\"}" "$ATOK")"
expect "aynı öğrenci aynı seansa 2. aktif → 409" 409 "$(pcode "/api/studio/class-sessions/$SESS1/bookings" "{\"studentProfileId\":\"$SG\"}" "$ATOK")"
# SG'nin SESS1 booking'ini bul + iptal et → tekrar booking yapılabilmeli
BKSG=$(curl -s "$BASE/api/studio/class-sessions/$SESS1/bookings" -H "Authorization: Bearer $ATOK" | jget id)
pcode "/api/studio/bookings/$BKSG/cancel" '{}' "$ATOK" >/dev/null
expect "iptal edilmiş booking sonrası aynı öğrenci tekrar booking" 200 "$(pcode "/api/studio/class-sessions/$SESS1/bookings" "{\"studentProfileId\":\"$SG\"}" "$ATOK")"

echo "--- Doluluk bilgisi ---"
LIST=$(curl -s "$BASE/api/studio/class-sessions?from=$FUT&to=$FUT" -H "Authorization: Bearer $ATOK")
expect "session listesinde isFull/bookedCount" yes "$(has "$LIST" '"bookedCount"')"

echo "--- Guest / Inactive / Self kuralları ---"
expect "Inactive öğrenci için staff booking → 409" 409 "$(pcode "/api/studio/class-sessions/$SESS2/bookings" "{\"studentProfileId\":\"$SI\"}" "$ATOK")"
G2=$(guest GS2); mkc "$G2"
expect "Guest için staff booking → 200" 200 "$(pcode "/api/studio/class-sessions/$SESS2/bookings" "{\"studentProfileId\":\"$G2\"}" "$ATOK")"
BKSELF=$(curl -s "$BASE/api/studio/class-sessions/$SESS2/bookings" -H "Authorization: Bearer $ATOK" | jget id)

echo "--- Öğrenci self-service KAPALI (Sprint 18b: admin-güdümlü) ---"
expect "Student self-booking → 410" 410 "$(pcode "/api/student/class-sessions/$SESS2/book" '{}' "$SATOK")"
expect "Student self-cancel → 410" 410 "$(pcode "/api/student/bookings/$BKSELF/cancel" '{}' "$SATOK")"
expect "Student /api/studio/bookings → 403" 403 "$(gcode /api/studio/bookings "$SATOK")"
expect "Student my-bookings (kendi programı) → 200" 200 "$(gcode /api/student/bookings "$SATOK")"

echo "--- Instructor scope ---"
expect "Instructor başka hocanın seansına booking → 403" 403 "$(pcode "/api/studio/class-sessions/$SESS1/bookings" "{\"studentProfileId\":\"$SG\"}" "$I2TOK")"

echo "--- Tenant izolasyonu (Owner C) ---"
CG=$(body "$(post /api/studio/students/guest "{\"fullName\":\"CG\",\"source\":\"Phone\"}" "$CTOK")" | jget id)
expect "C, A'nın seansına booking → 404" 404 "$(pcode "/api/studio/class-sessions/$SESS2/bookings" "{\"studentProfileId\":\"$CG\"}" "$CTOK")"
expect "C, A'nın booking'ini get → 404" 404 "$(gcode "/api/studio/bookings/$BKSELF" "$CTOK")"
expect "C, A'nın booking'ini iptal → 404" 404 "$(pcode "/api/studio/bookings/$BKSELF/cancel" '{}' "$CTOK")"

echo "--- Paralel yarış (cap=1 seansa 5 eşzamanlı booking → tam 1 başarılı) ---"
RG=(); for i in 1 2 3 4 5; do RG[$i]=$(guest "RG$i"); mkc "${RG[$i]}"; done
for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/studio/class-sessions/$RACE/bookings" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"studentProfileId\":\"${RG[$i]}\"}" > /tmp/race_$i &
done
wait
SUCCESS=0; CONFLICT=0
for i in 1 2 3 4 5; do c=$(cat /tmp/race_$i); [ "$c" = "200" ] && SUCCESS=$((SUCCESS+1)); [ "$c" = "409" ] && CONFLICT=$((CONFLICT+1)); done
rm -f /tmp/race_*
expect "paralel: tam 1 başarılı" 1 "$SUCCESS"
expect "paralel: 4 çakışma (409)" 4 "$CONFLICT"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 4 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 4 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
