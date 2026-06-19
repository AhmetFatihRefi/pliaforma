#!/usr/bin/env bash
# Pliaforma — Sprint 9 smoke (bildirim: ders/ödeme hatırlatma üretimi, idempotency, in-app okuma, yetki, tenant).
# Kullanım: scripts/sprint9-smoke.sh [BASE]  (vars. http://localhost:5132)
# Not: hatırlatma stüdyo-yerel zamana (Europe/Istanbul) bağlı; smoke tarihleri TZ=Europe/Istanbul ile hesaplanır.
# Worker Dev'de kapalı (determinism); bildirimler manuel trigger ile üretilir.
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
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
IST(){ TZ='Europe/Istanbul' date "$@"; }
TOMOR=$(IST -d '+1 day' +%Y-%m-%d)

# --- Stüdyolar + hoca + grup ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S9A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s9a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S9C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s9c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
post /api/auth/register "{\"fullName\":\"i1\",\"email\":\"i1+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
vrf "i1+$TS@pf.local"; RR=$(post /api/studio/memberships/invite "{\"email\":\"i1+$TS@pf.local\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); M=$(body "$RR" | jget membershipId); I1TOK=$(login "i1+$TS@pf.local"); post "/api/members/$M/accept" '{}' "$I1TOK" >/dev/null; I1P=$(ipid "i1+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$1\",\"capacity\":5}" "$ATOK")" | jget id; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
mkactive(){ local g; g=$(guest "$1"); post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$1+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; echo "$g"; }
sbook(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }

SA=$(mkactive "sa"); SATOK=$(login "sa+$TS@pf.local")
SB=$(mkactive "sb"); SBTOK=$(login "sb+$TS@pf.local")
GG=$(guest "GG")
# Sprint 18b: Regular booking sözleşme ister → booked öğrencilere geniş sözleşme (auto-resolve).
mkc(){ curl -s -o /dev/null -X POST "$BASE/api/studio/students/$1/contracts" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"2020-01-01\",\"endDate\":\"2035-01-01\",\"totalLessonCount\":100,\"makeupAllowance\":5,\"price\":0,\"isIndividualDefault\":false}"; }
mkc "$SA"; mkc "$SB"; mkc "$GG"

# Yaklaşan dersler (Istanbul +20..+23h → 25h penceresi içinde, hepsi gelecek)
STA=$(mksess "$(IST -d '+20 hours' +%Y-%m-%dT%H:%M:00)"); BA=$(sbook "$STA" "$SA")            # geçerli → reminder
STG=$(mksess "$(IST -d '+21 hours' +%Y-%m-%dT%H:%M:00)"); sbook "$STG" "$GG" >/dev/null         # guest → reminder yok
STB=$(mksess "$(IST -d '+22 hours' +%Y-%m-%dT%H:%M:00)"); BB=$(sbook "$STB" "$SB"); pcode "/api/studio/bookings/$BB/cancel" '{}' "$ATOK" >/dev/null  # iptal booking
STD=$(mksess "$(IST -d '+23 hours' +%Y-%m-%dT%H:%M:00)"); sbook "$STD" "$SB" >/dev/null; pcode "/api/studio/class-sessions/$STD/cancel" '{}' "$ATOK" >/dev/null  # iptal session

# Ödemeler (SA): due-soon (yarın), overdue (2020), paid
post "/api/studio/students/$SA/payments" "{\"amount\":500,\"dueDate\":\"$TOMOR\"}" "$ATOK" >/dev/null
post "/api/studio/students/$SA/payments" "{\"amount\":700,\"dueDate\":\"2020-01-01\"}" "$ATOK" >/dev/null
PJ=$(post "/api/studio/students/$SA/payments" "{\"amount\":300,\"dueDate\":\"2020-01-01\"}" "$ATOK"); PPAID=$(body "$PJ" | jget id)
pcode "/api/studio/payments/$PPAID/mark-paid" '{}' "$ATOK" >/dev/null

echo "--- Manuel trigger üretimi ---"
R1=$(post /api/studio/notifications/run-reminders '{}' "$ATOK")
expect "run-reminders → 200" 200 "$(code "$R1")"
expect "classReminders=1 (guest/iptal hariç)" 1 "$(body "$R1" | jnum classReminders)"
expect "paymentDueSoon=1" 1 "$(body "$R1" | jnum paymentDueSoon)"
expect "paymentOverdue=1 (paid hariç)" 1 "$(body "$R1" | jnum paymentOverdue)"

echo "--- Idempotency ---"
R2=$(post /api/studio/notifications/run-reminders '{}' "$ATOK")
expect "ikinci trigger → total=0 (duplicate yok)" 0 "$(body "$R2" | jnum total)"

echo "--- In-app okuma (SA) ---"
NL=$(get /api/notifications "$SATOK")
expect "SA bildirim listesi ClassReminder içerir" yes "$(has "$NL" '"type":"ClassReminder"')"
expect "SA bildirim PaymentOverdue içerir" yes "$(has "$NL" '"type":"PaymentOverdue"')"
expect "SA unread-count=3" 3 "$(get /api/notifications/unread-count "$SATOK" | jnum data)"
NID=$(echo "$NL" | jget id)
expect "bildirim okundu → 200" 200 "$(pcode "/api/notifications/$NID/read" '{}' "$SATOK")"
expect "okuma sonrası unread-count=2" 2 "$(get /api/notifications/unread-count "$SATOK" | jnum data)"
expect "read-all → 200" 200 "$(pcode /api/notifications/read-all '{}' "$SATOK")"
expect "read-all sonrası unread-count=0" 0 "$(get /api/notifications/unread-count "$SATOK" | jnum data)"

echo "--- Yetki ---"
expect "Student manager trigger → 403" 403 "$(pcode /api/studio/notifications/run-reminders '{}' "$SATOK")"
expect "Instructor manager trigger → 403" 403 "$(pcode /api/studio/notifications/run-reminders '{}' "$I1TOK")"
expect "Owner trigger → 200" 200 "$(pcode /api/studio/notifications/run-reminders '{}' "$ATOK")"

echo "--- Tenant / başka kullanıcı ---"
expect "SB başka kullanıcının bildirimini görmez (boş/sadece kendi)" no "$(has "$(get /api/notifications "$SBTOK")" '"type":"ClassReminder"')"
expect "SB, SA bildirimini okundu → 404" 404 "$(pcode "/api/notifications/$NID/read" '{}' "$SBTOK")"
expect "C kullanıcısı bildirim listesi → 200 (kendi, boş)" 200 "$(gcode /api/notifications "$CTOK")"
expect "C, SA bildirimini okundu → 404" 404 "$(pcode "/api/notifications/$NID/read" '{}' "$CTOK")"
expect "C trigger kendi stüdyosu → 200" 200 "$(pcode /api/studio/notifications/run-reminders '{}' "$CTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 9 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 9 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
