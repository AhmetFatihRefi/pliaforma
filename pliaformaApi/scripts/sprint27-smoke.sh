#!/usr/bin/env bash
# Pliaforma — Sprint 27 smoke (Instructor Makeup Request & Pending Schedule Blocks):
#  - Talep create: Instructor yalnız KENDİ aktif sözleşmeli + kredili öğrencisi (başka hoca adına / sözleşmesiz / kredisiz → red).
#  - Çift yönlü blok: pending talep gerçek hoca+öğrenci saatini bloklar; reject/withdraw boşaltır.
#  - Approve: gerçek Makeup ClassSession+ClassBooking materyalize + ledger Use(−1); reject/withdraw ledger DOKUNMAZ.
#  - Yetki: Instructor approve/reject yapamaz (403); owner approve/reject/edit-approve (200); student salt görür, create 403.
#  NOT: Bu kod tabanında başarılı create/komut Ok (200) döner (ApiResponse zarfı), 201 değil.
# Postgres container gerekli. Kullanım: scripts/sprint27-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S27 $TS\",\"ownerFullName\":\"O\",\"email\":\"o27+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2); I1TOK=$(login "i1+$TS@pf.local"); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
# sözleşme (instructor "kendi bağlamı" şartı): aktif + bu hoca + bu ders tipi + tarih aralığı.
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$2\",\"startDate\":\"$(date +%F)\",\"endDate\":\"$(date -d '+30 days' +%F)\",\"totalLessonCount\":8,\"makeupAllowance\":2,\"price\":0,\"isIndividualDefault\":false}" "$ATOK")" | jget id; }
grant(){ pcode "/api/studio/students/$1/makeup-credits/grant" "{\"amount\":$2,\"notes\":\"smoke\"}" "$ATOK" >/dev/null; }
bal(){ gbody "/api/studio/students/$1/makeup-credits" "$ATOK" | grep -o '"balance":[0-9-]*' | head -1; }
# talep oluştur (instructor): full post çıktısı (code+body). $3=token (default I1TOK), $4=ekstra json (örn requestedInstructorProfileId).
mkreq(){ post /api/studio/makeup-requests "{\"studentProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\"${4:+,$4}}" "${3:-$I1TOK}"; }
mksess(){ post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"$2\"}" "$ATOK"; }
D3=$(date -d "+3 days" +%Y-%m-%d)

SA=$(guest SA); mkc "$SA" "$I1P" >/dev/null; grant "$SA" 1          # I1 sözleşme + 1 kredi
SB=$(guest SB); mkc "$SB" "$I1P" >/dev/null; grant "$SB" 1          # I1 sözleşme + 1 kredi
SC=$(guest SC); mkc "$SC" "$I2P" >/dev/null; grant "$SC" 1          # I2 sözleşme + 1 kredi
SNOC=$(guest SNOC); grant "$SNOC" 1                                 # sözleşme YOK + kredi (sözleşme şartı testi)
SNOCRED=$(guest SNOCRED); mkc "$SNOCRED" "$I1P" >/dev/null          # I1 sözleşme + kredi YOK (rezerve testi)

echo "--- 1) Talep create: sözleşme + kredi + 'kendi profili' kuralları ---"
RA=$(mkreq "$SA" "${D3}T09:00:00"); expect "Instructor aktif sözleşmeli+kredili öğrenci talebi → 200" 200 "$(code "$RA")"
REQ_A=$(body "$RA" | jget id)
expect "talep durum Pending" yes "$(has "$(body "$RA")" '"status":"Pending"')"
expect "Instructor sözleşmesiz öğrenci talebi → 403" 403 "$(code "$(mkreq "$SNOC" "${D3}T09:00:00")")"
expect "Instructor başka hoca adına talep → 403" 403 "$(code "$(mkreq "$SA" "${D3}T20:00:00" "$I1TOK" "\"requestedInstructorProfileId\":\"$I2P\"")")"
expect "Instructor kredisiz (rezerve=0) öğrenci talebi → 409" 409 "$(code "$(mkreq "$SNOCRED" "${D3}T09:00:00")")"
expect "Aynı krediyle 2. pending talep → 409 (rezerve tek-kullanım)" 409 "$(code "$(mkreq "$SA" "${D3}T11:00:00")")"

echo "--- 2) Çift yönlü blok: pending talep gerçek hoca+öğrenci saatini bloklar ---"
expect "Pending saatine gerçek ders (aynı hoca I1, 09:00) → 409 (hoca bloğu)" 409 "$(pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${D3}T09:00:00\"}" "$ATOK")"
SI2=$(body "$(mksess "$I2P" "${D3}T09:00:00")" | jget id)   # farklı hoca aynı saat → 200 (hoca bloğu yok)
expect "Farklı hoca (I2) aynı saatte gerçek ders → 200" 200 "$([ -n "$SI2" ] && echo 200 || echo x)"
expect "Pending öğrencisini (SA) aynı saatteki gerçek derse booking → 409 (öğrenci bloğu)" 409 "$(pcode "/api/studio/class-sessions/$SI2/bookings" "{\"studentProfileId\":\"$SA\"}" "$ATOK")"

echo "--- 3) Görünürlük: Instructor kendi; Owner tümü ---"
RC=$(mkreq "$SC" "${D3}T10:00:00" "$I2TOK"); REQ_C=$(body "$RC" | jget id)
expect "I2 kendi talebini oluşturur → 200" 200 "$(code "$RC")"
expect "Instructor(I1) talep listesi → 200" 200 "$(gcode /api/studio/makeup-requests "$I1TOK")"
expect "I1 listesinde KENDİ talebi (REQ_A) var" yes "$(has "$(gbody /api/studio/makeup-requests "$I1TOK")" "$REQ_A")"
expect "I1 listesinde I2'nin talebi (REQ_C) YOK" no "$(has "$(gbody /api/studio/makeup-requests "$I1TOK")" "$REQ_C")"
expect "Owner listesinde I2'nin talebi (REQ_C) VAR (tümü)" yes "$(has "$(gbody /api/studio/makeup-requests "$ATOK")" "$REQ_C")"

echo "--- 4) Reject slotu boşaltır + ledger DOKUNMAZ ---"
RR=$(mkreq "$SB" "${D3}T13:00:00"); REQ_R=$(body "$RR" | jget id)
expect "SB talebi (13:00) → 200" 200 "$(code "$RR")"
expect "Owner reject → 200" 200 "$(pcode "/api/studio/makeup-requests/$REQ_R/reject" '{"decisionNote":"uygun degil"}' "$ATOK")"
expect "Reject sonrası SB ledger bakiyesi DEĞİŞMEZ (1)" yes "$(has "$(bal "$SB")" '"balance":1')"
expect "Reject sonrası slot boşaldı: I1 13:00 gerçek ders → 200" 200 "$(code "$(mksess "$I1P" "${D3}T13:00:00")")"

echo "--- 5) Withdraw: kendi pending (200) / başka hoca (403) / ledger DOKUNMAZ ---"
RW=$(mkreq "$SB" "${D3}T15:00:00"); REQ_W=$(body "$RW" | jget id)
expect "SB talebi (15:00) → 200" 200 "$(code "$RW")"
expect "Instructor BAŞKA hocanın pending'ini (REQ_C) withdraw → 403" 403 "$(pcode "/api/studio/makeup-requests/$REQ_C/withdraw" '{}' "$I1TOK")"
expect "Instructor KENDİ pending'ini withdraw → 200" 200 "$(pcode "/api/studio/makeup-requests/$REQ_W/withdraw" '{}' "$I1TOK")"
expect "Withdraw sonrası SB ledger bakiyesi DEĞİŞMEZ (1)" yes "$(has "$(bal "$SB")" '"balance":1')"
expect "Withdraw sonrası slot boşaldı: I1 15:00 gerçek ders → 200" 200 "$(code "$(mksess "$I1P" "${D3}T15:00:00")")"

echo "--- 6) Yetki + pending bir ClassSession DEĞİLDİR ---"
expect "Instructor approve → 403" 403 "$(pcode "/api/studio/makeup-requests/$REQ_A/approve" '{}' "$I1TOK")"
expect "Instructor reject → 403" 403 "$(pcode "/api/studio/makeup-requests/$REQ_A/reject" '{}' "$I1TOK")"
expect "Pending id'si ClassSession değil → confirm 404" 404 "$(pcode "/api/studio/class-sessions/$REQ_A/confirm" '{}' "$ATOK")"
expect "Pending id'si ClassSession değil → attendance(GET) 404" 404 "$(gcode "/api/studio/class-sessions/$REQ_A/attendance" "$ATOK")"

echo "--- 7) Owner approve → gerçek Makeup ders + ledger Use(−1) ---"
expect "Approve öncesi SA ledger bakiyesi = 1" yes "$(has "$(bal "$SA")" '"balance":1')"
RAP=$(post "/api/studio/makeup-requests/$REQ_A/approve" '{}' "$ATOK")
expect "Owner approve → 200" 200 "$(code "$RAP")"
expect "Approve durum Approved" yes "$(has "$(body "$RAP")" '"status":"Approved"')"
expect "Approve gerçek seansı bağladı (resultingClassSessionId dolu)" yes "$(has "$(body "$RAP")" '"resultingClassSessionId":"')"
MS=$(body "$RAP" | jget resultingClassSessionId)
expect "Materyalize seans Makeup tipinde" yes "$(has "$(gbody "/api/studio/class-sessions/$MS" "$ATOK")" '"sessionKind":"Makeup"')"
expect "Materyalize seans bireysel (IsIndividual)" yes "$(has "$(gbody "/api/studio/class-sessions/$MS" "$ATOK")" '"isIndividual":true')"
expect "Approve sonrası SA ledger bakiyesi = 0 (Use −1)" yes "$(has "$(bal "$SA")" '"balance":0')"

echo "--- 8) Edit-approve (tarih/saat değiştirerek onayla) ---"
RE=$(mkreq "$SB" "${D3}T16:00:00"); REQ_E=$(body "$RE" | jget id)
expect "SB talebi (16:00) → 200" 200 "$(code "$RE")"
RED=$(post "/api/studio/makeup-requests/$REQ_E/approve" "{\"startAt\":\"${D3}T16:30:00\"}" "$ATOK")
expect "Owner edit-approve (16:00 → 16:30) → 200" 200 "$(code "$RED")"
MSE=$(body "$RED" | jget resultingClassSessionId)
expect "Materyalize seans 16:30'da" yes "$(has "$(gbody "/api/studio/class-sessions/$MSE" "$ATOK")" "${D3}T16:30:00")"
expect "Edit-approve sonrası SB ledger bakiyesi = 0 (Use −1)" yes "$(has "$(bal "$SB")" '"balance":0')"

echo "--- 9) Student salt görür; create yapamaz ---"
post "/api/studio/students/$SB/convert-to-user" "{\"email\":\"sb+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
STOK=$(login "sb+$TS@pf.local")
expect "Student kendi telafi taleplerini görür → 200" 200 "$(gcode /api/student/makeup-requests "$STOK")"
expect "Student görünümünde onaylı telafisi var (Approved)" yes "$(has "$(gbody /api/student/makeup-requests "$STOK")" '"status":"Approved"')"
expect "Student talep oluşturamaz → 403" 403 "$(pcode /api/studio/makeup-requests "{\"studentProfileId\":\"$SB\",\"classTypeId\":\"$CT\",\"startAt\":\"${D3}T18:00:00\"}" "$STOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 27 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 27 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
