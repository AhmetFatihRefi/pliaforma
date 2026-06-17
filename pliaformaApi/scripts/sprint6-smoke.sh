#!/usr/bin/env bash
# Pliaforma — Sprint 6 smoke (yoklama/katılım: zaman kuralı, batch upsert, mazeretli/geç-iptal kredisi,
# özet, yetki + tenant izolasyonu). Kullanım: scripts/sprint6-smoke.sh [BASE]  (vars. http://localhost:5132)
# Not: "başlamış seans" gerektiği için seans API'den ileri tarihle açılıp psql ile geçmişe çekilir
# (create endpoint geçmiş seansı reddeder). Postgres container'ı otomatik tespit edilir (native/compose).
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":-\?[0-9]*" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putj(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
bcredit(){ curl -s "$BASE/api/studio/students/$1/makeup-credits" -H "Authorization: Bearer $ATOK" | jnum balance; }
D=$(date -d "+3 days" +%Y-%m-%d)

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
if [ -z "$PGC" ]; then echo "Postgres container bulunamadı (backdate için gerekli)."; exit 2; fi
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='2020-01-01 10:00:00', \"EndAt\"='2020-01-01 11:00:00' WHERE \"Id\"='$1';" >/dev/null; }

# --- Stüdyolar + hocalar + grup ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S6A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s6a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S6C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s6c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
for n in i1 i2; do post /api/auth/register "{\"fullName\":\"$n\",\"email\":\"$n+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null; done
inv(){ RR=$(post /api/studio/memberships/invite "{\"email\":\"$1+$TS@pf.local\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); M=$(body "$RR" | jget membershipId); T=$(login "$1+$TS@pf.local"); post "/api/members/$M/accept" '{}' "$T" >/dev/null; body "$RR" | jget instructorProfileId; }
I1P=$(inv i1); I2P=$(inv i2); I1TOK=$(login "i1+$TS@pf.local"); I2TOK=$(login "i2+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"${1:-$I1P}\",\"classTypeId\":\"$CT\",\"startAt\":\"${D}T$2:00\",\"capacity\":5}" "$ATOK")" | jget id; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
mkactive(){ local g; g=$(guest "$1"); post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$1+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; echo "$g"; }
sbook(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }

echo "--- Gelecek seans → 409 ---"
FS=$(mksess "$I1P" "09:00"); GF=$(guest "GF"); BF=$(sbook "$FS" "$GF")
expect "gelecek seans yoklama → 409" 409 "$(putcode "/api/studio/class-sessions/$FS/attendance" "{\"items\":[{\"bookingId\":\"$BF\",\"status\":\"Present\"}]}" "$ATOK")"

echo "--- Geçmiş seans → 200, batch, Present/Absent kredi vermez ---"
PS=$(mksess "$I1P" "10:00"); GA=$(guest "GA"); GB=$(guest "GB")
BA=$(sbook "$PS" "$GA"); BB=$(sbook "$PS" "$GB")
backdate "$PS"
GV=$(curl -s "$BASE/api/studio/class-sessions/$PS/attendance" -H "Authorization: Bearer $ATOK")
expect "geçmiş seans canMark=true" yes "$(has "$GV" '"canMark":true')"
R=$(putj "/api/studio/class-sessions/$PS/attendance" "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Present\"},{\"bookingId\":\"$BB\",\"status\":\"Absent\"}]}" "$ATOK")
expect "batch yoklama → 200" 200 "$(code "$R")"
expect "Present işaretlendi" yes "$(has "$(body "$R")" '"status":"Present"')"
expect "Present kredi vermez (GA bakiye 0)" 0 "$(bcredit "$GA")"
expect "Absent kredi vermez (GB bakiye 0)" 0 "$(bcredit "$GB")"

echo "--- Upsert: ikinci işaretleme update, duplicate yok ---"
putj "/api/studio/class-sessions/$PS/attendance" "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Excused\"}]}" "$ATOK" >/dev/null
GV2=$(curl -s "$BASE/api/studio/class-sessions/$PS/attendance" -H "Authorization: Bearer $ATOK")
expect "GA artık Excused (update)" yes "$(has "$GV2" '"status":"Excused"')"
expect "booking başına tek attendance (no duplicate)" 1 "$(sql "SELECT count(*) FROM \"Attendances\" WHERE \"ClassBookingId\"='$BA' AND \"IsDeleted\"=false;")"

echo "--- Excused + grant → +1; ikinci kez kredi vermez ---"
putj "/api/studio/class-sessions/$PS/attendance" "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Excused\",\"grantMakeupCredit\":true}]}" "$ATOK" >/dev/null
expect "Excused+grant → GA bakiye 1" 1 "$(bcredit "$GA")"
putj "/api/studio/class-sessions/$PS/attendance" "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Excused\",\"grantMakeupCredit\":true}]}" "$ATOK" >/dev/null
expect "ikinci kez grant kredi vermez (bakiye hâlâ 1)" 1 "$(bcredit "$GA")"

echo "--- LateCancel + grant → +1 ---"
putj "/api/studio/bookings/$BB/attendance" "{\"status\":\"LateCancel\",\"grantMakeupCredit\":true}" "$ATOK" >/dev/null
expect "LateCancel+grant → GB bakiye 1" 1 "$(bcredit "$GB")"

echo "--- Cancelled booking → yoklama alınmaz ---"
PS2=$(mksess "$I1P" "11:00"); GC=$(guest "GC"); BC=$(sbook "$PS2" "$GC")
pcode "/api/studio/bookings/$BC/cancel" '{}' "$ATOK" >/dev/null
backdate "$PS2"
expect "iptal booking için yoklama → 409" 409 "$(putcode "/api/studio/class-sessions/$PS2/attendance" "{\"items\":[{\"bookingId\":\"$BC\",\"status\":\"Present\"}]}" "$ATOK")"

echo "--- Cancelled session → yoklama alınmaz ---"
PS3=$(mksess "$I1P" "12:00"); GD=$(guest "GD"); BD=$(sbook "$PS3" "$GD")
pcode "/api/studio/class-sessions/$PS3/cancel" '{}' "$ATOK" >/dev/null
backdate "$PS3"
expect "iptal seans için yoklama → 409" 409 "$(putcode "/api/studio/class-sessions/$PS3/attendance" "{\"items\":[{\"bookingId\":\"$BD\",\"status\":\"Present\"}]}" "$ATOK")"

echo "--- Özet (staff + student self) ---"
SUM=$(curl -s "$BASE/api/studio/students/$GA/attendance/summary?month=2020-01" -H "Authorization: Bearer $ATOK")
expect "GA özet excusedCount=1" 1 "$(echo "$SUM" | jnum excusedCount)"
expect "GA özet totalMarked=1" 1 "$(echo "$SUM" | jnum totalMarked)"
# Student self: aktif öğrenci self-book → backdate → staff Present → kendi özeti
SE=$(mkactive "se"); SETOK=$(login "se+$TS@pf.local")
PSE=$(mksess "$I1P" "13:00"); BE=$(body "$(post "/api/student/class-sessions/$PSE/book" '{}' "$SETOK")" | jget id)
backdate "$PSE"
putj "/api/studio/class-sessions/$PSE/attendance" "{\"items\":[{\"bookingId\":\"$BE\",\"status\":\"Present\"}]}" "$ATOK" >/dev/null
expect "student kendi attendance → 200" 200 "$(gcode /api/student/attendance "$SETOK")"
expect "student kendi geçmişinde kayıt var" yes "$(has "$(curl -s "$BASE/api/student/attendance" -H "Authorization: Bearer $SETOK")" '"status":"Present"')"
SSUM=$(curl -s "$BASE/api/student/attendance/summary?month=2020-01" -H "Authorization: Bearer $SETOK")
expect "student özet presentCount=1" 1 "$(echo "$SSUM" | jnum presentCount)"

echo "--- Instructor kendi seansı işaretler; başka hocanınkine 403 ---"
PSI=$(mksess "$I1P" "14:00"); GI=$(guest "GI"); BI=$(sbook "$PSI" "$GI"); backdate "$PSI"
expect "I1 kendi seansı yoklama → 200" 200 "$(putcode "/api/studio/class-sessions/$PSI/attendance" "{\"items\":[{\"bookingId\":\"$BI\",\"status\":\"Present\"}]}" "$I1TOK")"
expect "I2 başka hocanın seansı yoklama görüntüle → 403" 403 "$(gcode "/api/studio/class-sessions/$PSI/attendance" "$I2TOK")"
expect "I2 başka hocanın seansı yoklama işaretle → 403" 403 "$(putcode "/api/studio/class-sessions/$PSI/attendance" "{\"items\":[{\"bookingId\":\"$BI\",\"status\":\"Absent\"}]}" "$I2TOK")"

echo "--- Tenant + Student engeli ---"
expect "C başka stüdyo seansı yoklama görüntüle → 404" 404 "$(gcode "/api/studio/class-sessions/$PS/attendance" "$CTOK")"
expect "C başka stüdyo seansı yoklama işaretle → 404" 404 "$(putcode "/api/studio/class-sessions/$PS/attendance" "{\"items\":[{\"bookingId\":\"$BA\",\"status\":\"Absent\"}]}" "$CTOK")"
expect "Student staff yoklama ucu → 403" 403 "$(gcode "/api/studio/class-sessions/$PS/attendance" "$SETOK")"
expect "Student staff özet ucu → 403" 403 "$(gcode "/api/studio/students/$GA/attendance/summary" "$SETOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 6 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 6 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
