#!/usr/bin/env bash
# Pliaforma — Güvenlik denetimi smoke (release-gate). Yetki + tenant izolasyonu + platform bypass + suspend
# davranışını TEK ÖZET olarak doğrular. Mevcut sprint smoke'larının güvenlik-kritik alt kümesidir.
# Kullanım: scripts/security-audit-smoke.sh [BASE]   (vars. http://localhost:5132)
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
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (PlatformAdmin tohumlama gerekli)."; exit 2; }
CT_BODY='{"name":"Mat","equipment":"Mat","capacityType":"Group","maxCapacity":5,"level":"AllLevels","purpose":"General","defaultDurationMinutes":60}'

# ---- Fixtures: stüdyo A (owner+I1+I2+SA), stüdyo B (owner+SB), PlatformAdmin ----
AR=$(post /api/auth/register-studio "{\"studioName\":\"SecA $TS\",\"ownerFullName\":\"OA\",\"email\":\"seca+$TS@pf.local\",\"password\":\"$PW\"}")
ATOK=$(body "$AR" | jget accessToken); AID=$(body "$AR" | jget studioId)
BR=$(post /api/auth/register-studio "{\"studioName\":\"SecB $TS\",\"ownerFullName\":\"OB\",\"email\":\"secb+$TS@pf.local\",\"password\":\"$PW\"}")
BTOK=$(body "$BR" | jget accessToken)

inv(){ post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$1+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
       local r; r=$(post /api/studio/memberships/invite "{\"email\":\"$1+$TS@pf.local\",\"role\":\"$2\"${3:+,\"rate\":$3}}" "$ATOK")
       local m; m=$(body "$r" | jget membershipId); local t; t=$(login "$1+$TS@pf.local")
       post "/api/members/$m/accept" '{}' "$t" >/dev/null; body "$r" | jget instructorProfileId; }
I1P=$(inv i1 Instructor 100); I1TOK=$(login "i1+$TS@pf.local")
I2P=$(inv i2 Instructor 100)
CTA=$(body "$(post /api/class-types "$CT_BODY" "$ATOK")" | jget id)
# A öğrencisi SA (user) + A booking/payment, ayrı seans (I1 vs I2)
gactive(){ local g; g=$(body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id)
           post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$1+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; echo "$g"; }
SA=$(gactive sa); SATOK=$(login "sa+$TS@pf.local")
S1=$(body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CTA\",\"startAt\":\"2026-12-01T10:00:00\",\"capacity\":5}" "$ATOK")" | jget id)
S2=$(body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CTA\",\"startAt\":\"2026-12-01T12:00:00\",\"capacity\":5}" "$ATOK")" | jget id)
BA=$(body "$(post "/api/studio/class-sessions/$S1/bookings" "{\"studentProfileId\":\"$SA\"}" "$ATOK")" | jget id)
PA=$(body "$(post "/api/studio/students/$SA/payments" "{\"amount\":100,\"dueDate\":\"2026-12-31\"}" "$ATOK")" | jget id)
DUE=$(body "$(post "/api/studio/students/$SA/payments" "{\"amount\":50,\"dueDate\":\"2020-01-01\"}" "$ATOK")" | jget id)
# B öğrencisi + booking/payment
SBp=$(body "$(post /api/studio/students/guest "{\"fullName\":\"sb\",\"source\":\"Phone\"}" "$BTOK")" | jget id)
PB=$(body "$(post "/api/studio/students/$SBp/payments" "{\"amount\":100,\"dueDate\":\"2026-12-31\"}" "$BTOK")" | jget id)
# PlatformAdmin
post /api/auth/register "{\"fullName\":\"padmin\",\"email\":\"secp+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"GlobalRole\"='PlatformAdmin' WHERE \"Email\"='secp+$TS@pf.local';" >/dev/null 2>&1
PTOK=$(login "secp+$TS@pf.local")
# SA bildirimi (reminder)
pcode /api/studio/notifications/run-reminders '{}' "$ATOK" >/dev/null
NID=$(get /api/notifications "$SATOK" | jget id)

echo "--- Cross-tenant (Owner başka stüdyo) ---"
# By-id uçlar cross-tenant 404 verir; list uçları tenant-filtreli → boş 200 (veri sızmaz). İkisini de kanıtla.
expect "Owner B, A'nın öğrencisini (by-id) görür → 404" 404 "$(gcode "/api/studio/students/$SA" "$BTOK")"
expect "Owner B, A'nın rezervasyonunu (by-id) görür → 404" 404 "$(gcode "/api/studio/bookings/$BA" "$BTOK")"
expect "Owner B, A ödemesini ödendi işaretler (by-id) → 404" 404 "$(pcode "/api/studio/payments/$PA/mark-paid" '{}' "$BTOK")"
expect "Owner A, B'nin öğrencisini (by-id) görür → 404" 404 "$(gcode "/api/studio/students/$SBp" "$ATOK")"
expect "Owner B, A öğrencisinin ödeme listesi → veri SIZMAZ (boş)" no "$(has "$(get "/api/studio/students/$SA/payments" "$BTOK")" "$PA")"

echo "--- Instructor scope ---"
expect "Instructor I1, başka hocanın (I2) seans rezervasyonları → 403" 403 "$(gcode "/api/studio/class-sessions/$S2/bookings" "$I1TOK")"
expect "Instructor finans ucu (payments) → 403" 403 "$(gcode /api/studio/payments "$I1TOK")"

echo "--- Student scope ---"
expect "Student staff öğrenci listesi → 403" 403 "$(gcode /api/studio/students "$SATOK")"
expect "Student staff student dashboard → 403" 403 "$(gcode "/api/studio/students/$SA/dashboard" "$SATOK")"
expect "Student kendi dashboard → 200" 200 "$(gcode /api/student/dashboard "$SATOK")"

echo "--- Platform / bypass ---"
expect "Owner platform overview → 403" 403 "$(gcode /api/platform/overview "$ATOK")"
expect "Instructor platform overview → 403" 403 "$(gcode /api/platform/overview "$I1TOK")"
expect "Student platform overview → 403" 403 "$(gcode /api/platform/overview "$SATOK")"
expect "PlatformAdmin platform overview → 200" 200 "$(gcode /api/platform/overview "$PTOK")"
expect "PlatformAdmin tenant read (GET /api/studio) bypass yok → 403" 403 "$(gcode /api/studio "$PTOK")"
expect "PlatformAdmin tenant write (POST class-types) bypass yok → 403" 403 "$(pcode /api/class-types "$CT_BODY" "$PTOK")"

echo "--- Notification cross-user ---"
expect "Student B, A öğrencisinin bildirimini okur → 404" 404 "$(pcode "/api/notifications/$NID/read" '{}' "$BTOK")"

echo "--- Suspend davranışı ---"
post "/api/platform/studios/$AID/suspend" '{"reason":"security audit"}' "$PTOK" >/dev/null
expect "Askıdaki stüdyo write (POST class-types) → 403" 403 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"
expect "Askıdaki stüdyo read (GET /api/studio) → 200" 200 "$(gcode /api/studio "$ATOK")"
post "/api/platform/studios/$AID/reactivate" '{}' "$PTOK" >/dev/null
expect "Reaktive sonrası write tekrar → 200" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSECURITY AUDIT SMOKE: TÜM KONTROLLER GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSECURITY AUDIT SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
