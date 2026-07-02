#!/usr/bin/env bash
# Pliaforma — Sprint 48 smoke (Security Defense-in-Depth Hardening).
#   ORTAK (her iki pilot modunda):
#     A) Rol düşürme: StudioAdmin token alir; Owner rolu Instructor'a ceker → ESKI token ile write (class-type)
#        VE hassas read (students) 403 (role mismatch fail-closed). Yeniden login → yeni token rol-uyumlu calisir.
#     B) Uyelikten cikarma: StudioAdmin token alir; Owner remove eder → ESKI token ile hassas read (students) 403
#        (Sprint 48 read-side re-check) ve write 403 (mevcut davranis).
#   PILOT ON (Pilot__SimplePeopleMode=true):
#     E) Pilot line id ile line-by-id mutator'lar (preferred-slots / preferences / makeup grant) 409 (500 DEGIL).
#   Not: C (staging email fail-fast) ayri bir startup testidir — smoke degil (bkz. validation raporu).
#        D (ContractResolver !IsPilot) olu kod; API'den ulasilamaz — build + sprint46/47 pilot testleri kapsar.
#   Postgres opsiyonel (yalniz E pilot-line id icin). ASCII-only. Kullanim: scripts/sprint48-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
skip(){ printf "  \033[33mSKIP\033[0m %s\n" "$1"; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jbool(){ echo "$1" | grep -o "\"$2\":\(true\|false\)" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
patchcode(){ curl -s -o /dev/null -w "%{http_code}" -X PATCH "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
sql(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null | tr -d '\r'; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
pilotLineId(){ sql "SELECT l.\"Id\" FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE c.\"StudentProfileId\"='$1' AND l.\"IsPilot\"=true LIMIT 1;" | tr -d '[:space:]'; }
TS=$(date +%s)

echo "== Sprint 48: Security Defense-in-Depth Hardening =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S48 $TS\",\"ownerFullName\":\"O\",\"email\":\"o48+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 2; }
FLAG=$(jbool "$(gbody /api/auth/me "$ATOK")" pilotSimplePeopleMode)
echo "-- pilotSimplePeopleMode = ${FLAG:-bos} --"

# mkAdmin $label -> globals AT (token) AMID (membershipId) AEMAIL
mkAdmin(){
  AEMAIL="$1+$TS@pf.local"
  post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$AEMAIL\",\"password\":\"$PW\"}" >/dev/null; vrf "$AEMAIL"
  AMID=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$AEMAIL\",\"role\":\"StudioAdmin\"}" "$ATOK")" | jget membershipId)
  local t=$(login "$AEMAIL"); post "/api/members/$AMID/accept" '{}' "$t" >/dev/null
  AT=$(login "$AEMAIL")   # accept SONRASI login → token studio_role=StudioAdmin tasir
}
CTBODY='{"name":"Grp","defaultDurationMinutes":60,"description":null}'

echo "--- A) Rol dusurme: eski token role-mismatch ile fail-closed ---"
mkAdmin adm48a; ATA=$AT; AMIDA=$AMID
expect "A baseline: StudioAdmin GET students -> 200" 200 "$(gcode /api/studio/students "$ATA")"
expect "A baseline: StudioAdmin POST class-type (write) -> 200" 200 "$(code "$(post /api/class-types "$CTBODY" "$ATA")")"
expect "A: Owner rol dusurur (StudioAdmin -> Instructor) -> 200" 200 "$(patchcode "/api/studio/memberships/$AMIDA/role" '{"role":"Instructor"}' "$ATOK")"
expect "A: ESKI token hassas READ (students) -> 403 (role mismatch)" 403 "$(gcode /api/studio/students "$ATA")"
expect "A: ESKI token WRITE (class-type) -> 403 (role mismatch)" 403 "$(code "$(post /api/class-types "$CTBODY" "$ATA")")"
NTA=$(login "$AEMAIL")   # yeniden login → yeni token Instructor rolu tasir (DB ile uyumlu)
expect "A: refresh/yeni token (Instructor) GET class-types -> 200 (rol uyumlu, calisir)" 200 "$(gcode /api/class-types "$NTA")"
expect "A: refresh/yeni token (Instructor) GET students -> 403 ([Authorize], guard degil)" 403 "$(gcode /api/studio/students "$NTA")"

echo "--- B) Uyelikten cikarma: eski token ile hassas read + write engellenir ---"
mkAdmin adm48b; ATB=$AT; AMIDB=$AMID
expect "B baseline: StudioAdmin GET students -> 200" 200 "$(gcode /api/studio/students "$ATB")"
expect "B: Owner uyeyi cikarir (remove) -> 200" 200 "$(pcode "/api/studio/memberships/$AMIDB/remove" '{}' "$ATOK")"
expect "B: ESKI token hassas READ (students) -> 403 (Sprint 48 read-side re-check)" 403 "$(gcode /api/studio/students "$ATB")"
expect "B: ESKI token WRITE (class-type) -> 403 (mevcut davranis)" 403 "$(code "$(post /api/class-types "$CTBODY" "$ATB")")"

# Owner/Admin normal akisi bozulmadi: Owner hala okur+yazar
expect "Regresyon: Owner GET students -> 200" 200 "$(gcode /api/studio/students "$ATOK")"
expect "Regresyon: Owner POST class-type -> 200" 200 "$(code "$(post /api/class-types "$CTBODY" "$ATOK")")"

if [ "$FLAG" = "true" ]; then
  echo "--- E) Pilot line by-id mutator'lari 409 (500 DEGIL) ---"
  # Instructor + pilot ogrenci + booking → gizli pilot line olusur
  IE="hoca48e+$TS@pf.local"
  post /api/auth/register "{\"fullName\":\"hoca48e\",\"email\":\"$IE\",\"password\":\"$PW\"}" >/dev/null; vrf "$IE"
  IMID=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$IE\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
  ITT=$(login "$IE"); post "/api/members/$IMID/accept" '{}' "$ITT" >/dev/null; ITOK=$(login "$IE")
  IPID=$(gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$IE\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/')
  CT=$(body "$(post /api/class-types "{\"name\":\"MatE\",\"defaultDurationMinutes\":60,\"description\":null}" "$ATOK")" | jget id)
  SP=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Pilot E48\"}" "$ITOK")" | jget id)
  SES=$(body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$IPID\",\"classTypeId\":\"$CT\",\"startAt\":\"$(date -d '+2 days 10:00' +%FT%T)\",\"capacity\":3,\"notes\":null}" "$ATOK")" | jget id)
  expect "E setup: pilot ogrenci booking -> 200" 200 "$(code "$(post "/api/studio/class-sessions/$SES/bookings" "{\"studentProfileId\":\"$SP\",\"studentContractLineId\":null,\"allowPastBooking\":false}" "$ITOK")")"
  if [ -n "$PGC" ]; then
    PLID=$(pilotLineId "$SP")
    if [ -n "$PLID" ]; then
      PSC=$(pcode "/api/studio/contract-lines/$PLID/preferred-slots" '{"dayOfWeek":"Monday","preferredStartTime":null,"notes":null}' "$ATOK")
      { [ "$PSC" = "409" ] || [ "$PSC" = "404" ]; } && ok "E: preferred-slots pilot line -> $PSC (500 degil)" || bad "E: preferred-slots -> $PSC (beklenen 404/409)"
      PRC=$(putcode "/api/studio/contract-lines/$PLID/preferences" '{"instructorProfileId":null,"preferredMaxCapacity":null,"preferredSlots":[{"dayOfWeek":"Monday","preferredStartTime":null,"notes":null}]}' "$ATOK")
      { [ "$PRC" = "409" ] || [ "$PRC" = "404" ]; } && ok "E: preferences pilot line -> $PRC (500 degil)" || bad "E: preferences -> $PRC (beklenen 404/409)"
      GRC=$(pcode "/api/studio/students/$SP/makeup-credits/grant" "{\"studentContractLineId\":\"$PLID\",\"amount\":1,\"notes\":null}" "$ATOK")
      { [ "$GRC" = "409" ] || [ "$GRC" = "404" ]; } && ok "E: makeup grant pilot line -> $GRC (500 degil)" || bad "E: makeup grant -> $GRC (beklenen 404/409)"
    else
      skip "E: pilot line id DB'de bulunamadi"
    fi
  else
    skip "E: psql yok — pilot line id alinamadi"
  fi
else
  echo "--- E) Pilot OFF: pilot-line mutator testi atlandi (flag kapali) ---"
  skip "E: yalniz Pilot__SimplePeopleMode=true stack'te calisir"
fi

echo
[ "$FAILED" = 0 ] && echo "== TUM TESTLER GECTI (mod: ${FLAG:-off}) ==" || echo "== BAZI TESTLER BASARISIZ (mod: ${FLAG:-off}) =="
exit $FAILED
