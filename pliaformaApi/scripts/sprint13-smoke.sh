#!/usr/bin/env bash
# Pliaforma — Sprint 13 smoke (abonelik yaşam döngüsü, plan kataloğu, tahsilat defteri, lisans kontrolü).
# Kullanım: scripts/sprint13-smoke.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
ge1(){ [ "${1:-0}" -ge 1 ] 2>/dev/null && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":-\?[0-9]*" | head -1 | sed "s/.*://"; }
jbool(){ grep -o "\"$1\":\(true\|false\)" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
put(){ curl -s -w "\n%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (psql arrange gerekli)."; exit 2; }
psql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" >/dev/null 2>&1; }
CT_BODY='{"name":"Mat","equipment":"Mat","capacityType":"Group","maxCapacity":5,"level":"AllLevels","purpose":"General","defaultDurationMinutes":60}'

# --- Fixtures: stüdyo A (Owner + StudioAdmin + Instructor + Student) + PlatformAdmin ---
AR=$(post /api/auth/register-studio "{\"studioName\":\"S13A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s13a+$TS@pf.local\",\"password\":\"$PW\"}")
ATOK=$(body "$AR" | jget accessToken); AID=$(body "$AR" | jget studioId)
# Sprint 17: davet kabulü doğrulanmış e-posta ister → kabulden önce verify.
inv(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null
       psql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$e' AND \"EmailVerifiedAt\" IS NULL;"
       local r m t; r=$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"$2\"${3:+,\"rate\":$3}}" "$ATOK")
       m=$(body "$r" | jget membershipId); t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; }
inv adm StudioAdmin; ADMTOK=$(login "adm+$TS@pf.local")
inv i1 Instructor 100; I1TOK=$(login "i1+$TS@pf.local")
GS=$(body "$(post /api/studio/students/guest '{"fullName":"sa","source":"Phone"}' "$ATOK")" | jget id)
post "/api/studio/students/$GS/convert-to-user" "{\"email\":\"sa+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
SATOK=$(login "sa+$TS@pf.local")
post /api/auth/register "{\"fullName\":\"padmin\",\"email\":\"s13p+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
psql "UPDATE \"Users\" SET \"GlobalRole\"='PlatformAdmin' WHERE \"Email\"='s13p+$TS@pf.local';"
PTOK=$(login "s13p+$TS@pf.local")

echo "--- Otomatik trial (register → default plan) ---"
SUB=$(get "/api/platform/studios/$AID/subscription" "$PTOK")
expect "yeni stüdyo effectiveStatus=Trialing" Trialing "$(echo "$SUB" | jget effectiveStatus)"
expect "yeni stüdyo isLicensed=true" true "$(echo "$SUB" | jbool isLicensed)"
expect "trial stüdyo yazma → 200 (lisanslı)" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"

echo "--- Plan CRUD yetki ---"
expect "PlatformAdmin plan listele → 200" 200 "$(gcode /api/platform/plans "$PTOK")"
expect "Owner plan oluştur → 403" 403 "$(pcode /api/platform/plans '{"name":"X","monthlyPrice":1,"currency":"TRY","billingPeriod":"Monthly","isCustomQuote":false,"isDefault":false,"isActive":true,"displayOrder":9}' "$ATOK")"
PLANJ=$(post /api/platform/plans '{"name":"S13 Test","tier":"T","monthlyPrice":300,"currency":"TRY","billingPeriod":"Monthly","introMonthlyPrice":100,"introPeriodCount":2,"isCustomQuote":false,"isDefault":false,"isActive":true,"displayOrder":9}' "$PTOK")
expect "PlatformAdmin plan oluştur → 200" 200 "$(code "$PLANJ")"
PLAN=$(body "$PLANJ" | jget id)

echo "--- Plan ata + ödeme kaydet (ledger + dönem + snapshot + intro) ---"
expect "plan ata → 200" 200 "$(code "$(put "/api/platform/studios/$AID/subscription/plan" "{\"planId\":\"$PLAN\"}" "$PTOK")")"
RP1=$(post "/api/platform/studios/$AID/subscription/record-payment" '{}' "$PTOK")
expect "record-payment → 200 + Active" Active "$(body "$RP1" | jget effectiveStatus)"
post "/api/platform/studios/$AID/subscription/record-payment" '{}' "$PTOK" >/dev/null   # 2. intro
post "/api/platform/studios/$AID/subscription/record-payment" '{}' "$PTOK" >/dev/null   # 3. standart
PAYS=$(get "/api/platform/studios/$AID/subscription/payments" "$PTOK")
expect "ledger intro fiyatı (100) içerir" yes "$(has "$PAYS" '"amount":100')"
expect "ledger standart fiyat (300) içerir (intro tükendi)" yes "$(has "$PAYS" '"amount":300')"
# Snapshot: planı 999 yap, tekrar tahsil → hâlâ 300 (abonelik snapshot)
put "/api/platform/plans/$PLAN" '{"name":"S13 Test","tier":"T","monthlyPrice":999,"currency":"TRY","billingPeriod":"Monthly","introMonthlyPrice":100,"introPeriodCount":2,"isCustomQuote":false,"isDefault":false,"isActive":true,"displayOrder":9}' "$PTOK" >/dev/null
post "/api/platform/studios/$AID/subscription/record-payment" '{}' "$PTOK" >/dev/null
PAYS2=$(get "/api/platform/studios/$AID/subscription/payments" "$PTOK")
expect "fiyat snapshot: yeni tahsilat 999 DEĞİL" no "$(has "$PAYS2" '"amount":999')"

echo "--- Owner cancel (cancel-at-period-end) ---"
CMINE=$(post /api/studio/subscription/cancel '{}' "$ATOK")
expect "Owner cancel → 200" 200 "$(code "$CMINE")"
expect "cancelAtPeriodEnd=true" true "$(body "$CMINE" | jbool cancelAtPeriodEnd)"
expect "effectiveStatus=CancelPending" CancelPending "$(body "$CMINE" | jget effectiveStatus)"
expect "cancel sonrası hâlâ lisanslı (isLicensed)" true "$(body "$CMINE" | jbool isLicensed)"
expect "cancel sonrası Owner hâlâ yazabilir → 200" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"

echo "--- Süre dolunca: yazma 403, okuma 200 ---"
psql "UPDATE \"PlatformSubscriptions\" SET \"CurrentPeriodEnd\"='2020-01-01', \"CancelAtPeriodEnd\"=false WHERE \"StudioId\"='$AID';"
expect "süresi dolmuş stüdyo yazma → 403" 403 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"
expect "süresi dolmuş stüdyo okuma (GET /api/studio) → 200" 200 "$(gcode /api/studio "$ATOK")"
expect "süresi dolmuş Owner abonelik görüntüleme → 200 (okuma açık)" 200 "$(gcode /api/studio/subscription "$ATOK")"
expect "Owner abonelik effectiveStatus=Expired" Expired "$(get /api/studio/subscription "$ATOK" | jget effectiveStatus)"

echo "--- Grace dönemi (7g) yazma açık ---"
G=$(TZ=UTC date -d '-3 days' +%Y-%m-%d 2>/dev/null || date -u -v-3d +%Y-%m-%d)
psql "UPDATE \"PlatformSubscriptions\" SET \"CurrentPeriodEnd\"='$G', \"Status\"='Active', \"CancelAtPeriodEnd\"=false WHERE \"StudioId\"='$AID';"
expect "grace içindeki stüdyo yazma → 200" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"

echo "--- Fail-open (abonelik yok) + görünür ---"
psql "DELETE FROM \"PlatformSubscriptions\" WHERE \"StudioId\"='$AID';"
expect "aboneliksiz stüdyo yazma → 200 (fail-open)" 200 "$(pcode /api/class-types "$CT_BODY" "$ATOK")"
MINE=$(get /api/studio/subscription "$ATOK")
expect "Owner görünüm hasSubscription=false" false "$(echo "$MINE" | jbool hasSubscription)"
expect "Owner görünüm effectiveStatus=FailOpenLicensed" FailOpenLicensed "$(echo "$MINE" | jget effectiveStatus)"
expect "overview missingSubscriptionCount ≥ 1 (görünür)" yes "$(ge1 "$(get /api/platform/overview "$PTOK" | jnum missingSubscriptionCount)")"

echo "--- Owner-only abonelik ucu ---"
expect "StudioAdmin /api/studio/subscription → 403" 403 "$(gcode /api/studio/subscription "$ADMTOK")"
expect "Instructor /api/studio/subscription → 403" 403 "$(gcode /api/studio/subscription "$I1TOK")"
expect "Student /api/studio/subscription → 403" 403 "$(gcode /api/studio/subscription "$SATOK")"
expect "Owner platform plan ucu → 403 (bypass yok)" 403 "$(gcode /api/platform/plans "$ATOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 13 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 13 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
