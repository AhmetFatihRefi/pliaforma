#!/usr/bin/env bash
# Pliaforma — Sprint 10 smoke (platform admin: overview/studios/detail, suspend/reactivate, abonelik, health, yetki, tenant).
# Kullanım: scripts/sprint10-smoke.sh [BASE]  (vars. http://localhost:5132)
# Not: PlatformAdmin için bir kullanıcı kaydedilip GlobalRole psql ile 'PlatformAdmin' yapılır, sonra tekrar login.
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
put(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (PlatformAdmin tohumlama gerekli)."; exit 2; }

CT_BODY='{"name":"Mat","equipment":"Mat","capacityType":"Group","maxCapacity":5,"level":"AllLevels","purpose":"General","defaultDurationMinutes":60}'

# --- Stüdyolar A, B ---
AR=$(post /api/auth/register-studio "{\"studioName\":\"S10A $TS\",\"ownerFullName\":\"OA\",\"email\":\"a+$TS@pf.local\",\"password\":\"$PW\"}")
ATOK=$(body "$AR" | jget accessToken); AID=$(body "$AR" | jget studioId)
BR=$(post /api/auth/register-studio "{\"studioName\":\"S10B $TS\",\"ownerFullName\":\"OB\",\"email\":\"b+$TS@pf.local\",\"password\":\"$PW\"}")
BTOK=$(body "$BR" | jget accessToken); BID=$(body "$BR" | jget studioId)

# Instructor + StudioAdmin + Student in A (403 kontrolleri için)
# Sprint 17: davet kabulü doğrulanmış e-posta ister → kabulden önce verify.
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
post /api/auth/register "{\"fullName\":\"i1\",\"email\":\"i1+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
vrf "i1+$TS@pf.local"
MI=$(body "$(post /api/studio/memberships/invite "{\"email\":\"i1+$TS@pf.local\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK")" | jget membershipId)
I1TOK=$(login "i1+$TS@pf.local"); post "/api/members/$MI/accept" '{}' "$I1TOK" >/dev/null
post /api/auth/register "{\"fullName\":\"adm\",\"email\":\"adm+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
vrf "adm+$TS@pf.local"
MA=$(body "$(post /api/studio/memberships/invite "{\"email\":\"adm+$TS@pf.local\",\"role\":\"StudioAdmin\"}" "$ATOK")" | jget membershipId)
ADMTOK=$(login "adm+$TS@pf.local"); post "/api/members/$MA/accept" '{}' "$ADMTOK" >/dev/null
GS=$(body "$(post /api/studio/students/guest "{\"fullName\":\"sa\",\"source\":\"Phone\"}" "$ATOK")" | jget id)
post "/api/studio/students/$GS/convert-to-user" "{\"email\":\"sa+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
SATOK=$(login "sa+$TS@pf.local")

# --- PlatformAdmin tohumla (register + psql GlobalRole + re-login) ---
post /api/auth/register "{\"fullName\":\"padmin\",\"email\":\"padmin+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"GlobalRole\"='PlatformAdmin' WHERE \"Email\"='padmin+$TS@pf.local';" >/dev/null 2>&1
PTOK=$(login "padmin+$TS@pf.local")

echo "--- PlatformAdmin yetki ---"
expect "PlatformAdmin overview → 200" 200 "$(gcode /api/platform/overview "$PTOK")"
expect "Owner overview → 403" 403 "$(gcode /api/platform/overview "$ATOK")"
expect "StudioAdmin overview → 403" 403 "$(gcode /api/platform/overview "$ADMTOK")"
expect "Instructor overview → 403" 403 "$(gcode /api/platform/overview "$I1TOK")"
expect "Student overview → 403" 403 "$(gcode /api/platform/overview "$SATOK")"

echo "--- PlatformAdmin tenant bypass yok ---"
expect "PlatformAdmin tenant read (GET /api/studio) → 403 (studio bağlamı yok)" 403 "$(gcode /api/studio "$PTOK")"
expect "PlatformAdmin tenant write (POST class-types) → 403" 403 "$(pcode /api/class-types "$CT_BODY" "$PTOK")"

echo "--- Overview / studios / detail ---"
OV=$(get /api/platform/overview "$PTOK")
expect "overview totalStudios alanı var" yes "$(has "$OV" '"totalStudios"')"
SL=$(get /api/platform/studios "$PTOK")
expect "studios listesi A'yı içerir" yes "$(has "$SL" "S10A $TS")"
expect "studios listesi B'yi içerir" yes "$(has "$SL" "S10B $TS")"
expect "studios?status=Active → 200" 200 "$(gcode "/api/platform/studios?status=Active" "$PTOK")"
expect "studios?search=S10A → A içerir" yes "$(has "$(get "/api/platform/studios?search=S10A" "$PTOK")" "S10A $TS")"
DA=$(get "/api/platform/studios/$AID" "$PTOK")
expect "detay A adı doğru" yes "$(has "$DA" "S10A $TS")"
expect "detay A owner e-postası doğru" yes "$(has "$DA" "a+$TS@pf.local")"
expect "başka stüdyo detayı (B) owner e-postası ayrı (tenant karışmaz)" yes "$(has "$(get "/api/platform/studios/$BID" "$PTOK")" "b+$TS@pf.local")"

echo "--- Abonelik (upsert / get / cancel / validation) ---"
SUB='{"planName":"Pro","tier":"T1","status":"Active","monthlyPrice":499.90,"currency":"TRY","billingPeriod":"Monthly","currentPeriodStart":"2026-06-01","currentPeriodEnd":"2026-06-30"}'
US=$(put "/api/platform/studios/$AID/subscription" "$SUB" "$PTOK")
expect "subscription update → 200" 200 "$(code "$US")"
expect "subscription planName=Pro" Pro "$(body "$US" | jget planName)"
expect "subscription get planName=Pro" Pro "$(get "/api/platform/studios/$AID/subscription" "$PTOK" | jget planName)"
BADSUB='{"planName":"X","status":"Active","monthlyPrice":10,"currency":"TRY","billingPeriod":"Monthly","currentPeriodStart":"2026-06-30","currentPeriodEnd":"2026-06-01"}'
expect "geçersiz dönem (bitiş<başlangıç) → 400" 400 "$(putcode "/api/platform/studios/$AID/subscription" "$BADSUB" "$PTOK")"
CA=$(post "/api/platform/studios/$AID/subscription/cancel" '{}' "$PTOK")
# Sprint 13: cancel artık "cancel-at-period-end" (Status değişmez; cancelAtPeriodEnd=true).
expect "subscription cancel → cancelAtPeriodEnd=true" true "$(body "$CA" | grep -o '\"cancelAtPeriodEnd\":\(true\|false\)' | head -1 | sed 's/.*://')"

echo "--- Suspend / Reactivate ---"
# Aktifken yazma çalışır (kontrol)
expect "aktif stüdyoda owner write → 200" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"
SU=$(post "/api/platform/studios/$AID/suspend" '{"reason":"odeme gecikmesi"}' "$PTOK")
expect "suspend → status Suspended" Suspended "$(body "$SU" | jget status)"
expect "suspend reason set" yes "$(has "$(get "/api/platform/studios/$AID" "$PTOK")" 'odeme gecikmesi')"
expect "askıdaki stüdyo owner write → 403" 403 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"
expect "askıdaki stüdyo owner read (GET /api/studio) → 200" 200 "$(gcode /api/studio "$ATOK")"
expect "askıdaki stüdyo reminder trigger → 403 (reminder üretilmez)" 403 "$(pcode /api/studio/notifications/run-reminders '{}' "$ATOK")"
RE=$(post "/api/platform/studios/$AID/reactivate" '{}' "$PTOK")
expect "reactivate → status Active" Active "$(body "$RE" | jget status)"
expect "reactivate sonrası suspend gerekçesi korunur" yes "$(has "$(get "/api/platform/studios/$AID" "$PTOK")" 'odeme gecikmesi')"
expect "reaktif stüdyoda owner write tekrar → 200" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"

echo "--- Health / 404 ---"
HS=$(get /api/platform/health-summary "$PTOK")
expect "health-summary → totalStudios alanı var" yes "$(has "$HS" '"totalStudios"')"
expect "health-summary reminderWorkerEnabled alanı var" yes "$(has "$HS" '"reminderWorkerEnabled"')"
expect "recent-activity → 200" 200 "$(gcode /api/platform/recent-activity "$PTOK")"
expect "olmayan stüdyo detayı → 404" 404 "$(gcode "/api/platform/studios/00000000-0000-0000-0000-000000000000" "$PTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 10 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 10 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
