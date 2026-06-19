#!/usr/bin/env bash
# Pliaforma — PİLOT DEMO seed (anlatım için gerçekçi veri). E2E seed'inden AYRIDIR; e2e-seed.sh'e dokunmaz.
#   E-posta öneki: demo-*  (e2e-* verisiyle karışmaz). Idempotent: önce demo-* verisini temizler.
# Üretir: 1 stüdyo · Owner · StudioAdmin · 2 instructor · 5 öğrenci · 3 ders tipi · grup + bireysel sözleşme ·
#   amendment'lı sözleşme · borçlu öğrenci · ödemesi tam öğrenci · telafi hakkı olan öğrenci · fiilî hoca ·
#   not-delivered · closure örneği.
# Kullanım: scripts/pilot-demo-seed.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
PW="DemoPass123!"
ok(){ printf "  \033[32m✓\033[0m %s\n" "$1"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

OWNER=demo-owner@pliaforma.local
ADMIN=demo-admin@pliaforma.local
I1=demo-ayse@pliaforma.local
I2=demo-mehmet@pliaforma.local
ZEYNEP=demo-zeynep@pliaforma.local   # ödemesi tam + kendi paneli (login'li öğrenci)

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (demo seed reset/backdate gerekli)."; exit 2; }
psql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" >/dev/null 2>&1; }

echo "== 0) Reset (yalnızca demo-* verisi) =="
psql "DELETE FROM \"Studios\" WHERE \"OwnerUserId\" IN (SELECT \"Id\" FROM \"Users\" WHERE \"Email\" LIKE 'demo-%');"
psql "DELETE FROM \"Users\" WHERE \"Email\" LIKE 'demo-%';"
ok "Önceki demo verisi temizlendi"

echo "== 1) Owner + stüdyo =="
OTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"Pliaforma Demo Studio\",\"ownerFullName\":\"Demo Owner\",\"email\":\"$OWNER\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$OTOK" ] && { echo "  HATA: Owner/stüdyo oluşturulamadı (auth rate-limit olabilir). Birkaç saniye sonra tekrar çalıştırın."; exit 1; }
ok "Owner: $OWNER (stüdyo: Pliaforma Demo Studio)"

echo "== 2) StudioAdmin + 2 Instructor (davet+kabul) =="
acceptMember(){ local email="$1" role="$2" resp m tok
  post /api/auth/register "{\"fullName\":\"$3\",\"email\":\"$email\",\"password\":\"$PW\"}" >/dev/null
  psql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$email';"
  resp=$(post /api/studio/memberships/invite "{\"email\":\"$email\",\"role\":\"$role\"}" "$OTOK")
  m=$(body "$resp" | jget membershipId); tok=$(login "$email")
  post "/api/members/$m/accept" '{}' "$tok" >/dev/null
  get /api/studio/memberships "$OTOK" | tr '{' '\n' | grep "\"email\":\"$email\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'
}
acceptMember "$ADMIN" StudioAdmin "Demo Admin" >/dev/null; ok "StudioAdmin: $ADMIN"
AYSE=$(acceptMember "$I1" Instructor "Ayse Hoca"); MEHMET=$(acceptMember "$I2" Instructor "Mehmet Hoca")
{ [ -z "$AYSE" ] || [ -z "$MEHMET" ]; } && { echo "  HATA: Hoca profili oluşmadı (auth rate-limit olabilir). Birkaç saniye sonra tekrar çalıştırın."; exit 1; }
ok "Instructor: $I1 (Ayşe, profil $AYSE)"; ok "Instructor: $I2 (Mehmet, profil $MEHMET)"

echo "== 3) Ders tipleri =="
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"equipment\":\"$2\",\"capacityType\":\"$3\",\"maxCapacity\":$4,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":$5}" "$OTOK")" | jget id; }
REF=$(ctype "Reformer Grup" "Reformer" "Group" 6 60)
MAT=$(ctype "Mat Grup" "Mat" "Group" 10 50)
PRI=$(ctype "Bireysel" "Reformer" "Private" 1 60)
ok "Reformer Grup · Mat Grup · Bireysel"

echo "== 4) Öğrenciler =="
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$OTOK")" | jget id; }
ZP=$(guest "Zeynep Demir"); post "/api/studio/students/$ZP/convert-to-user" "{\"email\":\"$ZEYNEP\",\"initialPassword\":\"$PW\"}" "$OTOK" >/dev/null
CAN=$(guest "Can Yilmaz"); ELIF=$(guest "Elif Kaya"); BURAK=$(guest "Burak Sahin"); DENIZ=$(guest "Deniz Ak")
ok "Zeynep (login'li) · Can · Elif · Burak · Deniz"

echo "== 5) Sözleşmeler + program =="
SD=$(date +%F); ED=$(date -d "+14 days" +%F)
mkc(){ body "$(post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$2\",\"instructorProfileId\":\"$3\",\"startDate\":\"$SD\",\"endDate\":\"$ED\",\"totalLessonCount\":$4,\"makeupAllowance\":$5,\"price\":$6,\"currency\":\"TRY\",\"isIndividualDefault\":$7}" "$OTOK")" | jget id; }
gen(){ post "/api/studio/contracts/$1/schedule/generate" "{\"slots\":$2,\"reuseExistingGroup\":true,\"individual\":null}" "$OTOK" >/dev/null; }
MWF18='[{"dayOfWeek":"Monday","startTime":"18:00:00"},{"dayOfWeek":"Wednesday","startTime":"18:00:00"},{"dayOfWeek":"Friday","startTime":"18:00:00"}]'
TT17='[{"dayOfWeek":"Tuesday","startTime":"17:00:00"},{"dayOfWeek":"Thursday","startTime":"17:00:00"}]'
TT19='[{"dayOfWeek":"Tuesday","startTime":"19:00:00"},{"dayOfWeek":"Thursday","startTime":"19:00:00"}]'
# Reformer grup (Ayşe, Pzt/Çar/Cum 18:00): Zeynep, Can, Burak PAYLAŞIR (cross-student reuse).
CZP=$(mkc "$ZP" "$REF" "$AYSE" 12 2 5000 false); gen "$CZP" "$MWF18"
CCAN=$(mkc "$CAN" "$REF" "$AYSE" 12 2 6000 false); gen "$CCAN" "$MWF18"
CBUR=$(mkc "$BURAK" "$REF" "$AYSE" 12 2 5000 false); gen "$CBUR" "$MWF18"
# Mat grup (Mehmet, Sal/Per 17:00): Deniz.
CDEN=$(mkc "$DENIZ" "$MAT" "$MEHMET" 8 1 3000 false); gen "$CDEN" "$TT17"
# Bireysel (Mehmet, Sal/Per 19:00): Elif.
CELI=$(mkc "$ELIF" "$PRI" "$MEHMET" 8 2 4000 true); gen "$CELI" "$TT19"
ok "Grup (Zeynep+Can+Burak paylaşır) · Mat (Deniz) · Bireysel (Elif)"

echo "== 6) Amendment (Elif sözleşmesine +4 ders +2000 TL) =="
post "/api/studio/contracts/$CELI/amendments" "{\"kind\":\"ExtraLessons\",\"additionalLessonCount\":4,\"additionalPrice\":2000,\"description\":\"Ek paket - yogun donem\"}" "$OTOK" >/dev/null
ok "Elif: 8→12 ders, +2000 TL ek ücret"

echo "== 7) Ödemeler (borçlu + ödemesi tam) =="
pay(){ local pid; pid=$(body "$(post "/api/studio/students/$1/payments" "{\"amount\":$2,\"dueDate\":\"$3\",\"studentContractId\":\"$4\"}" "$OTOK")" | jget id); [ "${5:-}" = paid ] && post "/api/studio/payments/$pid/mark-paid" '{}' "$OTOK" >/dev/null; }
pay "$ZP" 5000 "$SD" "$CZP" paid                      # Zeynep: tam ödeme (borç 0)
pay "$CAN" 2000 "$SD" "$CCAN" paid                     # Can: 6000'den 2000 ödendi → 4000 borç
pay "$CAN" 4000 "$(date -d '+30 days' +%F)" "$CCAN"    # kalan 4000 vadeli (Pending)
ok "Zeynep borç 0 (tam ödendi) · Can 4000 borç (kısmi)"

echo "== 8) Fiilî hoca + not-delivered + telafi (absence) =="
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$2\",\"startAt\":\"$3\"}" "$OTOK")" | jget id; }
book(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$OTOK")" | jget id; }
D5=$(date -d "+5 days" +%Y-%m-%d)
# Fiilî hoca: Ayşe'nin bir dersinde fiilî hoca Mehmet.
SUB=$(mksess "$AYSE" "$REF" "${D5}T20:00:00"); book "$SUB" "$ZP" >/dev/null
post "/api/studio/class-sessions/$SUB/actual-instructor" "{\"actualInstructorProfileId\":\"$MEHMET\"}" "$OTOK" >/dev/null
ok "Fiilî hoca örneği (planlanan Ayşe → fiilî Mehmet)"
# Not-delivered: Mehmet'in bir dersi işlenemedi → öğrenciye telafi.
ND=$(mksess "$MEHMET" "$MAT" "${D5}T21:00:00"); book "$ND" "$DENIZ" >/dev/null
post "/api/studio/class-sessions/$ND/not-delivered" '{}' "$OTOK" >/dev/null
ok "Not-delivered örneği (Deniz'e telafi)"
# Telafi (absence): Burak geçmiş derste devamsız → +1 telafi.
ABS=$(mksess "$AYSE" "$REF" "${D5}T22:00:00"); BB=$(book "$ABS" "$BURAK"); psql "UPDATE \"ClassSessions\" SET \"StartAt\"='2024-03-04 10:00:00', \"EndAt\"='2024-03-04 11:00:00' WHERE \"Id\"='$ABS';"
# Tekil yoklama ucu PUT'tur (POST → 405). Absent → sözleşme telafi hakkı (allowance>0) → +1.
curl -s -o /dev/null -X PUT "$BASE/api/studio/bookings/$BB/attendance" -H 'Content-Type: application/json' -H "Authorization: Bearer $OTOK" -d '{"status":"Absent"}'
ok "Devamsızlık → telafi örneği (Burak +1)"

echo "== 9) Closure (kapanış) örneği =="
DCL=$(date -d "+10 days" +%Y-%m-%d)
post /api/studio/closures "{\"title\":\"Resmi tatil\",\"startAt\":\"${DCL}T00:00:00\",\"endAt\":\"${DCL}T23:59:00\",\"actionType\":\"CancelSessions\"}" "$OTOK" >/dev/null
ok "Closure örneği ($DCL — Resmi tatil)"

echo "== 10) Bildirimler =="
post /api/studio/notifications/run-reminders '{}' "$OTOK" >/dev/null
ok "Hatırlatma bildirimleri üretildi"

echo
printf "\033[36mPİLOT DEMO SEED TAMAM ✅\033[0m  (tüm parolalar: %s)\n" "$PW"
echo "Owner=$OWNER  StudioAdmin=$ADMIN  Instructor=$I1, $I2  Öğrenci(login)=$ZEYNEP"
echo "Demo: grup+bireysel sözleşme · Elif amendment · Can 4000 borç / Zeynep ödendi · Burak telafi · fiilî hoca · not-delivered · closure"
