#!/usr/bin/env bash
# Pliaforma — Sprint 46 smoke (Pilot Simple People Mode).
# Flag durumunu /auth/me'den (pilotSimplePeopleMode) OKUR ve ona gore dallanir:
#   OFF (default): Instructor guest-create/minimal -> 403; Owner tam akis calisir (full mode bozulmaz).
#   ON  (Pilot__SimplePeopleMode=true): Instructor ad-soyad ogrenci ekler; minimal listeye erisir; pilot ogrenci
#       Regular derse eklenir (gizli pilot line lazy olusur, IsPilot=true, finans query'lerinde GIZLI); kapasite/
#       cakisma/dup calisir; Sprint 43 move calisir.
# Her iki modda: Owner guest-create (email/phone'suz) -> UserId null; convert-to-user Id'yi korur + duplicate email reddi.
# Bu betik iki kez calistirilmali: once flag ON backend, sonra flag OFF backend. ASCII-only. psql opsiyonel (IsPilot dogrulamasi).
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$2" | grep -q "$3" && ok "$1" || bad "$1 (icermiyor: $3)"; }
hasnot(){ echo "$2" | grep -q "$3" && bad "$1 (icermemeli: $3)" || ok "$1"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ echo "$1" | grep -o "\"$2\":-\\?[0-9.]*" | head -1 | sed "s/\"$2\"://"; }
jbool(){ echo "$1" | grep -o "\"$2\":\(true\|false\)" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres' | head -1)
vrf(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
pilotLineCount(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "SELECT count(*) FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE c.\"StudentProfileId\"='$1' AND l.\"IsPilot\"=true;" | tr -d '[:space:]'; }
TS=$(date +%s)
S1=$(date -d "+2 days 10:00" +%FT%T); S2=$(date -d "+3 days 10:00" +%FT%T); OV=$(date -d "+2 days 10:30" +%FT%T); CAPT=$(date -d "+4 days 10:00" +%FT%T)
ipid(){ gbody "/api/studio/memberships" "$2" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 46: Pilot Simple People Mode =="
# --- Studio A (owner + 2 instructor) ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S46 $TS\",\"ownerFullName\":\"O\",\"email\":\"o46+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 2; }
FLAG=$(jbool "$(gbody /api/auth/me "$ATOK")" pilotSimplePeopleMode)
echo "-- Tespit edilen pilotSimplePeopleMode = ${FLAG:-bos} --"

mkInstr(){ # $1 email-label -> echoes "IA_TOKEN IA_ID" via globals
  local e="$1+$TS@pf.local"
  post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
  local M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
  local T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null
  RIT=$(login "$e")   # accept SONRASI yeniden login -> token studio bağlamını (studioId/role claim) taşır
  RIA=$(ipid "$e" "$ATOK")
}
mkInstr hoca46a; ITA=$RIT; IA=$RIA
mkInstr hoca46b; ITB=$RIT; IB=$RIA
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guestOwner(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\"}" "$ATOK")"; }   # sadece ad-soyad (source vermeden)
mkSession(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\",\"capacity\":$3,\"notes\":null}" "$ATOK")" | jget id; }
bookRaw(){ post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\",\"studentContractLineId\":null,\"allowPastBooking\":false}" "${3:-$ATOK}"; }

echo "--- Ortak: Owner ad-soyad (email/phone yok) ogrenci -> UserId null ---"
SO=$(guestOwner "Ayse Owner46")
SOID=$(echo "$SO" | jget id)
expect "Owner guest create -> 200 govde id var" 1 "$([ -n "$SOID" ] && echo 1 || echo 0)"
GSO=$(gbody "/api/studio/students/$SOID" "$ATOK")
hasnot "Owner guest: UserId null (userId key JSON'da yok)" "$GSO" '"userId"'
has    "Owner guest: Status Guest" "$GSO" '"status":"Guest"'

echo "--- Ortak: convert-to-user Id'yi korur + duplicate email reddi ---"
CE="conv46+$TS@pf.local"
CV=$(post "/api/studio/students/$SOID/convert-to-user" "{\"email\":\"$CE\",\"initialPassword\":\"$PW\"}" "$ATOK")
expect "convert-to-user -> 200" 200 "$(code "$CV")"
has    "convert sonrasi StudentProfile.Id AYNI" "$(body "$CV")" "\"id\":\"$SOID\""
SO2=$(guestOwner "Dup Email46"); SO2ID=$(echo "$SO2" | jget id)
expect "ayni email ile 2. convert -> reddedilir (409)" 409 "$(code "$(post "/api/studio/students/$SO2ID/convert-to-user" "{\"email\":\"$CE\",\"initialPassword\":\"$PW\"}" "$ATOK")")"

if [ "$FLAG" = "true" ]; then
  echo "=== MOD: PILOT ON ==="
  echo "--- Instructor ad-soyad ogrenci ekler (flag ON) ---"
  IGR=$(post /api/studio/students/guest "{\"fullName\":\"Pilot Ogrenci A\"}" "$ITA")
  expect "Instructor guest create -> 200" 200 "$(code "$IGR")"
  SP=$(body "$IGR" | jget id)
  GSP=$(gbody "/api/studio/students/$SP" "$ATOK")
  hasnot "Pilot ogrenci UserId null" "$GSP" '"userId"'
  hasnot "Pilot ogrenci membership yok (studioMembershipId key yok)" "$GSP" '"studioMembershipId"'

  echo "--- Instructor minimal liste (tum studio, PII yok) ---"
  ML=$(gbody "/api/studio/students/minimal" "$ITA")
  expect "minimal -> 200" 200 "$(gcode "/api/studio/students/minimal" "$ITA")"
  has    "minimal: pilot ogrenci adi var" "$ML" 'Pilot Ogrenci A'
  has    "minimal: owner-eklenen ogrenci de gorunur (tum studio)" "$ML" 'Dup Email46'
  hasnot "minimal: telefon alani yok" "$ML" '"phone"'
  hasnot "minimal: email alani yok" "$ML" '"email"'
  hasnot "minimal: healthNotes alani yok" "$ML" '"healthNotes"'

  echo "--- Pilot ogrenci Regular derse eklenir (gizli pilot line lazy) ---"
  SES1=$(mkSession "$IA" "$S1" 3)
  BK=$(bookRaw "$SES1" "$SP" "$ITA")   # instructor kendi seansina ekler
  expect "Instructor kendi seansina pilot ogrenci ekler -> 200" 200 "$(code "$BK")"
  LINES=$(gbody "/api/studio/students/$SP/contract-lines" "$ATOK")
  has "pilot line finans/contract query'sinde GIZLI (data bos)" "$(echo "$LINES" | tr -d '[:space:]')" '"data":\[\]'
  if [ -n "$PGC" ]; then expectn "DB: pilot student icin IsPilot=true line >=1" 1 "$([ "$(pilotLineCount "$SP")" -ge 1 ] && echo 1 || echo 0)"; else echo "  (psql yok; IsPilot DB dogrulamasi atlandi)"; fi

  echo "--- Ikinci booking ayni pilot line'i yeniden kullanir (yeni line acmaz) ---"
  SES2=$(mkSession "$IA" "$S2" 3)
  expect "2. Regular booking (ayni classType) -> 200" 200 "$(code "$(bookRaw "$SES2" "$SP" "$ITA")")"
  if [ -n "$PGC" ]; then expectn "DB: hala TEK pilot line (reuse)" 1 "$(pilotLineCount "$SP")"; fi

  echo "--- Kapasite (dolu -> 409) ---"
  CAPS=$(mkSession "$IA" "$CAPT" 1)
  SPc=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Kapasite B\"}" "$ATOK")" | jget id)
  expect "cap=1 ilk ekleme -> 200" 200 "$(code "$(bookRaw "$CAPS" "$SP" "$ATOK")")"
  expect "cap dolu 2. ekleme -> 409" 409 "$(code "$(bookRaw "$CAPS" "$SPc" "$ATOK")")"

  echo "--- Cakisma: ayni ogrenci ortusen 2. derse -> 409 ---"
  SESov=$(mkSession "$IB" "$OV" 3)   # S1 ile ortusur (farkli hoca)
  expect "ortusen derse ayni ogrenci -> 409" 409 "$(code "$(bookRaw "$SESov" "$SP" "$ATOK")")"

  echo "--- Dup: ayni ogrenci ayni seansa 2. kez -> 409 ---"
  expect "dup booking -> 409" 409 "$(code "$(bookRaw "$SES1" "$SP" "$ATOK")")"

  echo "--- Sprint 43 move: pilot ogrenci baska derse tasinir ---"
  BID=$(body "$(bookRaw "$(mkSession "$IA" "$(date -d '+6 days 10:00' +%FT%T)" 2)" "$SPc" "$ATOK")" | jget id)
  SRC=$(gbody "/api/studio/bookings/$BID" "$ATOK")   # kaynak sessionId booking'ten al
  SRCSES=$(echo "$SRC" | jget classSessionId)
  TGT=$(mkSession "$IA" "$(date -d '+7 days 10:00' +%FT%T)" 2)
  MV=$(post "/api/studio/class-sessions/$SRCSES/bookings/move" "{\"bookingIds\":[\"$BID\"],\"targetSessionId\":\"$TGT\"}" "$ATOK")
  expect "move pilot ogrenci -> 200" 200 "$(code "$MV")"

  echo "--- Dashboard: pilot line kalan/gecikmis toplamlarini SISIRMEZ ---"
  DASH=$(gbody "/api/studio/dashboard?month=$(date +%Y-%m)" "$ATOK")
  expectn "dashboard pendingAmount 0 (pilot line gizli, gercek borc yok)" 0 "$(jnum "$DASH" pendingAmount)"
  expectn "dashboard overdueAmount 0" 0 "$(jnum "$DASH" overdueAmount)"

else
  echo "=== MOD: PILOT OFF (default) ==="
  echo "--- Instructor guest create -> 403 ---"
  expect "Instructor guest create -> 403" 403 "$(pcode /api/studio/students/guest "{\"fullName\":\"X\"}" "$ITA")"
  echo "--- Instructor minimal liste -> 403 ---"
  expect "Instructor minimal -> 403" 403 "$(gcode /api/studio/students/minimal "$ITA")"
  echo "--- Full mode: Owner akisi bozulmadi (guest create calisir) ---"
  expect "Owner guest create -> basarili (id dondu)" 1 "$([ -n "$(guestOwner "OwnerOff46" | jget id)" ] && echo 1 || echo 0)"
  echo "--- Instructor kendi seansina bile booking eklemez (flag OFF) -> 403 ---"
  SESoff=$(mkSession "$IA" "$S1" 3); SPoff=$(echo "$(guestOwner "Off Booking")" | jget id)
  expect "Instructor booking (flag off) -> 403" 403 "$(code "$(bookRaw "$SESoff" "$SPoff" "$ITA")")"
fi

echo
[ "$FAILED" = 0 ] && echo "== TUM TESTLER GECTI (mod: ${FLAG:-off}) ==" || echo "== BAZI TESTLER BASARISIZ (mod: ${FLAG:-off}) =="
exit $FAILED
