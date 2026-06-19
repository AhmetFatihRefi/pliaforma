#!/usr/bin/env bash
# Pliaforma — Sprint 18a smoke (sözleşme-tabanlı program, ADDITIVE):
#  - Öğrenci sözleşmesi oluştur (ders tipi, hoca, tarih aralığı, ders hakkı, ücret).
#  - Sözleşmeden program üret: Pzt/Prş 18:00 → 16 ders (session+booking), StudentContractId bağlı.
#  - Available aşımı engeli; ders süresi ders tipinden (EndAt-Start=60); mevcut gruba bağlanma.
#  - Çakışma: aynı hoca aynı saat → 409; FARKLI hoca aynı saat → 200; aynı öğrenci çakışma → 409.
#  - Bireysel seansa 2. öğrenci → 409; grup → çok öğrenci.
# Postgres container gerekli (profil/sözleşme/booking sayımı). Kullanım: scripts/sprint18a-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":[0-9]*" | head -1 | sed "s/\"$1\"://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }

# --- Stüdyo + 2 hoca (davet+kabul) + ders tipi (60 dk) + öğrenciler ---
AEMAIL="s18o+$TS@pf.local"; ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S18 $TS\",\"ownerFullName\":\"O\",\"email\":\"$AEMAIL\",\"password\":\"$PW\"}")" | jget accessToken)
invacc(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
I1P=$(invacc i1); I2P=$(invacc i2)
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
S1=$(guest "Stu1"); S2=$(guest "Stu2"); S3=$(guest "Stu3"); S4=$(guest "Stu4")

SD=$(date +%F); ED=$(date -d "+90 days" +%F)
mkcontract(){ post "/api/studio/students/$1/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$2\",\"startDate\":\"$SD\",\"endDate\":\"$ED\",\"totalLessonCount\":16,\"makeupAllowance\":2,\"price\":4000,\"isIndividualDefault\":$3,\"notes\":null}" "$ATOK"; }
gen(){ post "/api/studio/contracts/$1/schedule/generate" "{\"slots\":[{\"dayOfWeek\":\"Monday\",\"startTime\":\"18:00:00\"},{\"dayOfWeek\":\"Thursday\",\"startTime\":\"18:00:00\"}],\"reuseExistingGroup\":$2,\"individual\":null}" "$ATOK"; }

echo "--- 1) Sözleşme oluştur (available=Total) ---"
RC=$(mkcontract "$S1" "$I1P" false); expect "sözleşme oluştur → 200" 200 "$(code "$RC")"
C1=$(body "$RC" | jget id)
expect "available = Total (16)" 16 "$(body "$RC" | jnum availableLessonCount)"

echo "--- 2) Sözleşmeden program: Pzt/Prş 18:00 → 16 ders ---"
RG=$(gen "$C1" false); expect "program üret → 200" 200 "$(code "$RG")"
expect "16 ders oluştu (createdCount)" 16 "$(body "$RG" | jnum createdCount)"
expect "DB: bu sözleşmeye 16 Booked booking" 16 "$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"StudentContractId\"='$C1' AND \"Status\"='Booked';")"
expect "DB: hepsi StudentContractId bağlı (16)" 16 "$(sql "SELECT count(*) FROM \"ClassBookings\" cb JOIN \"ClassSessions\" cs ON cs.\"Id\"=cb.\"ClassSessionId\" WHERE cb.\"StudentContractId\"='$C1' AND cs.\"SessionKind\"='Regular';")"
expect "ders süresi ders tipinden (EndAt-Start=60dk)" 60 "$(sql "SELECT DISTINCT (EXTRACT(EPOCH FROM (cs.\"EndAt\"-cs.\"StartAt\"))/60)::int FROM \"ClassBookings\" cb JOIN \"ClassSessions\" cs ON cs.\"Id\"=cb.\"ClassSessionId\" WHERE cb.\"StudentContractId\"='$C1' LIMIT 1;")"
AVL=$(curl -s "$BASE/api/studio/contracts/$C1" -H "Authorization: Bearer $ATOK" | jnum availableLessonCount); expect "available artık 0" 0 "$AVL"

echo "--- 3) Ders hakkı aşımı engeli ---"
RG2=$(gen "$C1" false); expect "tekrar üret → 0 yeni ders (cap)" 0 "$(body "$RG2" | jnum createdCount)"
# Manuel seans (aynı hoca, FARKLI saat: Cumartesi 12:00 — geçmiş değil) + sözleşmeyle booking → ders hakkı yok → 409
SATX=$(date -d "next saturday 12:00" +%Y-%m-%dT%H:%M:%S)
SES_OV=$(body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$SATX\"}" "$ATOK")" | jget id)
expect "sözleşme hakkı bitti → staff-book 409" 409 "$(pcode /api/studio/class-sessions/$SES_OV/bookings "{\"studentProfileId\":\"$S1\",\"studentContractId\":\"$C1\"}" "$ATOK")"

echo "--- 4) Mevcut gruba bağlanma (reuse) ---"
RC2=$(mkcontract "$S2" "$I1P" false); C2=$(body "$RC2" | jget id)
SCNT_BEFORE=$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"InstructorProfileId\"='$I1P' AND \"ClassTypeId\"='$CT';")
RG3=$(gen "$C2" true)
expect "reuse ile 16 ders" 16 "$(body "$RG3" | jnum createdCount)"
expect "yeni seans açılmadı (newSessionCount=0)" 0 "$(body "$RG3" | jnum newSessionCount)"
expect "reused=16" 16 "$(body "$RG3" | jnum reusedSessionCount)"
SCNT_AFTER=$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"InstructorProfileId\"='$I1P' AND \"ClassTypeId\"='$CT';")
expect "seans sayısı değişmedi (gruba bağlandı)" "$SCNT_BEFORE" "$SCNT_AFTER"

echo "--- 5) Çakışma: aynı hoca aynı saat → 409; farklı hoca aynı saat → 200; aynı öğrenci → 409 ---"
MONX=$(sql "SELECT to_char(cs.\"StartAt\",'YYYY-MM-DD\"T\"HH24:MI:SS') FROM \"ClassBookings\" cb JOIN \"ClassSessions\" cs ON cs.\"Id\"=cb.\"ClassSessionId\" WHERE cb.\"StudentContractId\"='$C1' ORDER BY cs.\"StartAt\" LIMIT 1;")
expect "aynı hoca aynı saat 2. seans → 409" 409 "$(pcode /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$MONX\"}" "$ATOK")"
R_I2=$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CT\",\"startAt\":\"$MONX\"}" "$ATOK")
expect "FARKLI hoca aynı saat → 200 (stüdyo-geneli engel yok)" 200 "$(code "$R_I2")"
SES_I2=$(body "$R_I2" | jget id)
expect "aynı öğrenci (S1) çakışan saatte 2. ders → 409" 409 "$(pcode /api/studio/class-sessions/$SES_I2/bookings "{\"studentProfileId\":\"$S1\"}" "$ATOK")"
mkcontract "$S3" "$I2P" false >/dev/null   # Sprint 18b: Regular booking sözleşme ister → S3'e sözleşme
expect "farklı öğrenci (S3) aynı saatte → 200" 200 "$(pcode /api/studio/class-sessions/$SES_I2/bookings "{\"studentProfileId\":\"$S3\"}" "$ATOK")"

echo "--- 6) Bireysel ders: 2. öğrenci engeli; grup: çok öğrenci ---"
RC3=$(mkcontract "$S3" "$I2P" true); C3=$(body "$RC3" | jget id)   # bireysel sözleşme
RG4=$(gen "$C3" false); expect "bireysel program üret → 16" 16 "$(body "$RG4" | jnum createdCount)"
IND_SES=$(sql "SELECT cb.\"ClassSessionId\" FROM \"ClassBookings\" cb JOIN \"ClassSessions\" cs ON cs.\"Id\"=cb.\"ClassSessionId\" WHERE cb.\"StudentContractId\"='$C3' AND cs.\"IsIndividual\"=true ORDER BY cs.\"StartAt\" LIMIT 1;")
expect "bireysel seansa 2. öğrenci (S4) → 409" 409 "$(pcode /api/studio/class-sessions/$IND_SES/bookings "{\"studentProfileId\":\"$S4\"}" "$ATOK")"
GRP_SES=$(sql "SELECT cb.\"ClassSessionId\" FROM \"ClassBookings\" cb JOIN \"ClassSessions\" cs ON cs.\"Id\"=cb.\"ClassSessionId\" WHERE cb.\"StudentContractId\"='$C1' AND cs.\"IsIndividual\"=false ORDER BY cs.\"StartAt\" DESC LIMIT 1;")
GCNT=$(sql "SELECT count(*) FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$GRP_SES' AND \"Status\"='Booked';")
expect "grup seansta birden çok öğrenci (>=2)" yes "$([ "$GCNT" -ge 2 ] && echo yes || echo no)"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 18a SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 18a SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
