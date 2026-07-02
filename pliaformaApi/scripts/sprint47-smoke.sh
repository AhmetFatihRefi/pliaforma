#!/usr/bin/env bash
# Pliaforma — Sprint 47 smoke (Pre-live Pilot Hardening): Sprint 46 pilot guard düzeltmeleri.
#   Flag durumunu /auth/me'den (pilotSimplePeopleMode) OKUR ve ona gore dallanir:
#   ON  (Pilot__SimplePeopleMode=true):
#     A) Guest ogrenci Regular derse eklenir (temel akis hala calisir — Sprint 46 regresyonu yok).
#     B) F1 duzeltmesi: gercek (UserId dolu) ogrencinin uygun line'i yoksa PILOT FALLBACK ALMAZ -> 409.
#     C) convert-to-user SONRASI ayni ogrenci de pilot fallback almaz (B ile ayni senaryo, donusum sonrasi).
#     D) Pilot header hicbir sozlesme yuzeyinde gorunmez (contracts list + student self contracts).
#     E) Yeni GERCEK line pilot header'in ALTINA yazilmaz (ResolveActiveHeaderAsync duzeltmesi).
#     F) Pilot line candidate/makeup listelerinde gorunmez.
#     G) Pilot line id ile update/cancel/payment -> 404/409 (500 DEGIL). [psql gerekir; yoksa atlanir]
#     H) Instructor AllowPastBooking=true -> 403; Owner/StudioAdmin icin degismez (200). [psql gerekir; yoksa atlanir]
#     I) Iptal edilmis (Cancelled) line borc ozetine (remaining/overdue) GIRMEZ.
#   OFF (default): full mode bozulmadi (owner guest+line+booking akisi calisir; instructor guest-create 403).
#   Postgres opsiyonel (IsPilot/pilot-line-id dogrulamasi + H backdate icin). ASCII-only.
# Kullanim: scripts/sprint47-smoke.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
skip(){ printf "  \033[33mSKIP\033[0m %s\n" "$1"; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$2" | grep -q "$3" && ok "$1" || bad "$1 (icermiyor: $3)"; }
hasnot(){ echo "$2" | grep -q "$3" && bad "$1 (icermemeli: $3)" || ok "$1"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ echo "$1" | grep -o "\"$2\":-\\?[0-9.]*" | head -1 | sed "s/\"$2\"://"; }
jbool(){ echo "$1" | grep -o "\"$2\":\(true\|false\)" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putj(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
sql(){ [ -n "$PGC" ] && docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null | tr -d '\r'; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1';" >/dev/null; }
pilotLineId(){ sql "SELECT l.\"Id\" FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE c.\"StudentProfileId\"='$1' AND l.\"IsPilot\"=true LIMIT 1;" | tr -d '[:space:]'; }
pilotHeaderId(){ sql "SELECT \"Id\" FROM \"StudentContracts\" WHERE \"StudentProfileId\"='$1' AND \"IsPilot\"=true LIMIT 1;" | tr -d '[:space:]'; }
D(){ date -d "+$1 days" +%F; }
TS=$(date +%s)
S1=$(date -d "+2 days 10:00" +%FT%T); S2=$(date -d "+3 days 10:00" +%FT%T); S3=$(date -d "+4 days 10:00" +%FT%T); S4=$(date -d "+5 days 10:00" +%FT%T); S5=$(date -d "+6 days 10:00" +%FT%T)
ipid(){ gbody "/api/studio/memberships" "$2" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

echo "== Sprint 47: Pre-live Pilot Hardening =="
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S47 $TS\",\"ownerFullName\":\"O\",\"email\":\"o47+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 2; }
FLAG=$(jbool "$(gbody /api/auth/me "$ATOK")" pilotSimplePeopleMode)
echo "-- Tespit edilen pilotSimplePeopleMode = ${FLAG:-bos} --"

mkInstr(){ # $1 email-label -> globals RIT (token) RIA (instructorProfileId)
  local e="$1+$TS@pf.local"
  post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"
  local M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId)
  local T=$(login "$e"); post "/api/members/$M/accept" '{}' "$T" >/dev/null
  RIT=$(login "$e"); RIA=$(ipid "$e" "$ATOK")
}
mkInstr hoca47a; ITA=$RIT; IA=$RIA
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guestOwner(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\"}" "$ATOK")"; }
mkSession(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\",\"capacity\":$3,\"notes\":null}" "$ATOK")" | jget id; }
bookRaw(){ post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\",\"studentContractLineId\":null,\"allowPastBooking\":${4:-false}}" "${3:-$ATOK}"; }
addlineRaw(){ post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$IA\",\"startDate\":\"$(D 0)\",\"endDate\":\"$(D 200)\",\"lessonCount\":$2,\"pricePerLesson\":$3,\"makeupAllowance\":1,\"dueDate\":\"$4\",\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null,\"notes\":null}]}" "$ATOK"; }
addline(){ body "$(addlineRaw "$1" "$2" "$3" "$4")" | jget id; }

if [ "$FLAG" = "true" ]; then
  echo "=== MOD: PILOT ON ==="

  echo "--- A) Guest ogrenci Regular derse eklenir (temel akis) ---"
  SP=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Pilot A47\"}" "$ITA")" | jget id)
  SESA=$(mkSession "$IA" "$S1" 3)
  expect "A: guest booking (instructor, kendi seansi) -> 200" 200 "$(code "$(bookRaw "$SESA" "$SP" "$ITA")")"
  if [ -n "$PGC" ]; then PLID=$(pilotLineId "$SP"); [ -n "$PLID" ] && ok "A: DB'de IsPilot=true line olustu" || bad "A: pilot line DB'de bulunamadi"; else skip "A: DB dogrulama (psql yok)"; fi

  echo "--- B/C) Gercek (UserId dolu) ogrenci: uygun line yoksa pilot fallback ALMAZ -> 409 ---"
  SR=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Gercek B47\"}" "$ATOK")" | jget id)
  CE="conv47+$TS@pf.local"
  CVCODE=$(code "$(post "/api/studio/students/$SR/convert-to-user" "{\"email\":\"$CE\",\"initialPassword\":\"$PW\"}" "$ATOK")")
  expect "B: convert-to-user -> 200" 200 "$CVCODE"
  SESB=$(mkSession "$IA" "$S2" 3)
  BRESP=$(bookRaw "$SESB" "$SR" "$ATOK")
  expect "B: gercek ogrenci (line yok) pilot fallback ALMAZ -> 409" 409 "$(code "$BRESP")"
  has "B: hata mesaji 'uygun aktif sozlesme satiri yok'" "$(body "$BRESP")" "uygun aktif s"
  if [ -n "$PGC" ]; then expectn "C: gercek ogrenci icin pilot line YOK (0)" 0 "$([ -z "$(pilotLineId "$SR")" ] && echo 0 || echo 1)"; else skip "C: DB dogrulama (psql yok)"; fi

  echo "--- D) Pilot header hicbir sozlesme yuzeyinde gorunmez ---"
  CONTRACTS=$(gbody "/api/studio/students/$SP/contracts" "$ATOK")
  has "D: staff contracts listesi -> data bos (pilot header gizli)" "$(echo "$CONTRACTS" | tr -d '[:space:]')" '"data":\[\]'
  SPCE="spconv47+$TS@pf.local"
  post /api/auth/register "{\"fullName\":\"SPU\",\"email\":\"$SPCE\",\"password\":\"$PW\"}" >/dev/null; vrf "$SPCE"
  post "/api/studio/students/$SP/convert-to-user" "{\"email\":\"$SPCE\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
  SPTOK=$(login "$SPCE")
  SELFCONTRACTS=$(gbody "/api/student/contracts" "$SPTOK")
  has "D: ogrenci self contracts -> data bos (pilot header gizli)" "$(echo "$SELFCONTRACTS" | tr -d '[:space:]')" '"data":\[\]'

  echo "--- E) Yeni GERCEK line pilot header'in ALTINA yazilmaz ---"
  if [ -n "$PGC" ]; then
    PHID=$(pilotHeaderId "$SP")
    NEWLINE=$(body "$(addlineRaw "$SP" 4 500 "$(D 20)")")
    NEWHID=$(echo "$NEWLINE" | jget studentContractId)
    [ -n "$NEWHID" ] && [ "$NEWHID" != "$PHID" ] && ok "E: yeni gercek line farkli (gercek) header'da (pilot header != $PHID)" || bad "E: yeni line pilot header'in ALTINA yazildi ($NEWHID == $PHID)"
  else
    skip "E: pilot header id dogrulama (psql yok)"
  fi

  echo "--- F) Pilot line candidate listesinde gorunmez ---"
  # NOT: SP burada kullanilmaz — D/E adimlarinda SP'ye GERCEK line + convert-to-user uygulandi (kasitli),
  # bu yuzden SP artik mesru sekilde candidate'te cikar (pilot line'dan degil, gercek line'dan). Ayri, SADECE
  # pilot line'i olan taze bir ogrenciyle test edilir.
  SPF=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Pilot F47\"}" "$ITA")" | jget id)
  SESF0=$(mkSession "$IA" "$S3" 3)
  expect "F: pilot ogrenci (SPF) pilot line ile booking -> 200" 200 "$(code "$(bookRaw "$SESF0" "$SPF" "$ITA")")"
  SESF=$(mkSession "$IA" "$(date -d '+8 days 10:00' +%FT%T)" 3)
  CANDS=$(gbody "/api/studio/class-sessions/$SESF/regular-candidates" "$ATOK")
  hasnot "F: regular-candidates pilot ogrenciyi (SPF) ICERMEZ" "$CANDS" "\"studentProfileId\":\"$SPF\""
  MUCANDS=$(gbody "/api/studio/class-sessions/$SESF/makeup-candidates" "$ATOK")
  hasnot "F: makeup-candidates pilot ogrenciyi (SPF) ICERMEZ" "$MUCANDS" "\"studentProfileId\":\"$SPF\""

  echo "--- G) Pilot line id ile update/cancel/payment -> 404/409 (500 DEGIL) ---"
  if [ -n "$PGC" ] && [ -n "${PLID:-}" ]; then
    UPC=$(putcode "/api/studio/contract-lines/$PLID" "{\"dueDate\":null,\"notes\":\"x\"}" "$ATOK")
    [ "$UPC" = "409" ] || [ "$UPC" = "404" ] && ok "G: pilot line update -> $UPC (500 degil)" || bad "G: pilot line update -> $UPC (beklenen 404/409)"
    CNC=$(pcode "/api/studio/contract-lines/$PLID/cancel" '{}' "$ATOK")
    [ "$CNC" = "409" ] || [ "$CNC" = "404" ] && ok "G: pilot line cancel -> $CNC (500 degil)" || bad "G: pilot line cancel -> $CNC (beklenen 404/409)"
    PAYC=$(pcode "/api/studio/students/$SP/payments" "{\"amount\":100,\"studentContractLineId\":\"$PLID\"}" "$ATOK")
    [ "$PAYC" = "409" ] || [ "$PAYC" = "404" ] && ok "G: pilot line'a odeme -> $PAYC (500 degil)" || bad "G: pilot line'a odeme -> $PAYC (beklenen 404/409)"
  else
    skip "G: pilot line id yok / psql yok — atlandi"
  fi

  echo "--- H) Instructor AllowPastBooking=true -> 403; Owner icin degismez ---"
  if [ -n "$PGC" ]; then
    SESH=$(mkSession "$IA" "$S4" 3)
    sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$(D -2)T12:00:00',\"EndAt\"='$(D -2)T13:00:00' WHERE \"Id\"='$SESH';" >/dev/null
    SPH=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Gecmis H47\"}" "$ATOK")" | jget id)
    expect "H: Instructor allowPastBooking=true -> 403" 403 "$(code "$(bookRaw "$SESH" "$SPH" "$ITA" true)")"
    expect "H: Owner allowPastBooking=true -> 200 (Sprint 42 degismedi)" 200 "$(code "$(bookRaw "$SESH" "$SPH" "$ATOK" true)")"
  else
    skip "H: seans geriye tarihleme (psql yok) — atlandi"
  fi

  echo "--- I) Iptal edilmis (Cancelled) line borc ozetine GIRMEZ ---"
  SI=$(body "$(post /api/studio/students/guest "{\"fullName\":\"Iptal I47\"}" "$ATOK")" | jget id)
  YDAY=$(date -d "-1 day" +%F)
  LI=$(addline "$SI" 4 1000 "$YDAY")
  DBEF=$(gbody "/api/studio/students/$SI/debt" "$ATOK")
  expectn "I: iptal ONCESI remaining=4000 (kontrol)" 4000 "$(jnum "$(echo "$DBEF" | tr -d '[:space:]' | grep -o '{"currency":"TRY"[^}]*}')" remaining)"
  CNCI=$(code "$(post "/api/studio/contract-lines/$LI/cancel" '{}' "$ATOK")")
  expect "I: (odemesiz) line iptal -> 200" 200 "$CNCI"
  DAFT=$(gbody "/api/studio/students/$SI/debt" "$ATOK")
  TRYOBJ=$(echo "$DAFT" | tr -d '[:space:]' | grep -o '{"currency":"TRY"[^}]*}')
  if [ -z "$TRYOBJ" ]; then
    ok "I: iptal SONRASI TRY currency grubu yok (remaining/overdue sifirlandi, rollup'tan cikti)"
  else
    expectn "I: iptal SONRASI remaining=0" 0 "$(jnum "$TRYOBJ" remaining)"
    expectn "I: iptal SONRASI overdue=0" 0 "$(jnum "$TRYOBJ" overdue)"
  fi

else
  echo "=== MOD: PILOT OFF (default) — full mode bozulmadi ==="
  echo "--- Instructor guest create -> 403 (degismedi) ---"
  expect "Instructor guest create -> 403" 403 "$(pcode /api/studio/students/guest "{\"fullName\":\"X\"}" "$ITA")"
  echo "--- Owner: guest + GERCEK line + booking akisi bozulmadi (B1 degisikligi full mode'u etkilemez) ---"
  SO=$(body "$(post /api/studio/students/guest "{\"fullName\":\"OwnerOff47\"}" "$ATOK")" | jget id)
  LO=$(addline "$SO" 4 500 "$(D 20)")
  expect "Owner: gercek line acildi" 1 "$([ -n "$LO" ] && echo 1 || echo 0)"
  SESO=$(mkSession "$IA" "$S5" 3)
  BOOKO=$(post "/api/studio/class-sessions/$SESO/bookings" "{\"studentProfileId\":\"$SO\",\"studentContractLineId\":\"$LO\",\"allowPastBooking\":false}" "$ATOK")
  expect "Owner: gercek line ile booking -> 200" 200 "$(code "$BOOKO")"
  echo "--- Instructor booking (flag off) -> 403 (degismedi) ---"
  expect "Instructor booking (flag off) -> 403" 403 "$(code "$(post "/api/studio/class-sessions/$SESO/bookings" "{\"studentProfileId\":\"$SO\",\"studentContractLineId\":null,\"allowPastBooking\":false}" "$ITA")")"
fi

echo
[ "$FAILED" = 0 ] && echo "== TUM TESTLER GECTI (mod: ${FLAG:-off}) ==" || echo "== BAZI TESTLER BASARISIZ (mod: ${FLAG:-off}) =="
exit $FAILED
