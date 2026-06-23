#!/usr/bin/env bash
# Pliaforma — Sprint 2 smoke (öğrenci/misafir akışı, dönüşüm, tenant izolasyonu, instructor scope, student engeli).
# Kullanım: scripts/sprint2-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
put(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

OWNA="owna+$TS@pf.local"; OWNC="ownc+$TS@pf.local"; I1="i1+$TS@pf.local"; I2="i2+$TS@pf.local"; EXIST="exist+$TS@pf.local"

# Aktörler
R=$(post /api/auth/register-studio "{\"studioName\":\"S2A $TS\",\"ownerFullName\":\"Owner A\",\"email\":\"$OWNA\",\"password\":\"$PW\"}")
ATOK=$(body "$R" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S2C $TS\",\"ownerFullName\":\"Owner C\",\"email\":\"$OWNC\",\"password\":\"$PW\"}")" | jget accessToken)
post /api/auth/register "{\"fullName\":\"Ins1\",\"email\":\"$I1\",\"password\":\"$PW\"}" >/dev/null
post /api/auth/register "{\"fullName\":\"Ins2\",\"email\":\"$I2\",\"password\":\"$PW\"}" >/dev/null
post /api/auth/register "{\"fullName\":\"Existing U\",\"email\":\"$EXIST\",\"password\":\"$PW\"}" >/dev/null
# İki hocayı davet + kabul. Sprint 17: davet kabulü doğrulanmış e-posta ister → kabulden önce verify.
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
for E in "$I1" "$I2"; do
  docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$E' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1
  M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$E\",\"role\":\"Instructor\",\"rate\":100}" "$ATOK")" | jget membershipId)
  T=$(login "$E"); post "/api/members/$M/accept" '{}' "$T" >/dev/null
done
I1TOK=$(login "$I1"); I2TOK=$(login "$I2")

echo "--- Misafir oluşturma + güncelle + liste/arama ---"
R=$(post /api/studio/students/guest "{\"fullName\":\"Ayse Guest\",\"phone\":\"555111\",\"source\":\"Phone\",\"healthNotes\":\"diz\"}" "$ATOK")
expect "create guest" 200 "$(code "$R")"; G1=$(body "$R" | jget id)
expect "guest status=Guest" yes "$(has "$(body "$R")" '"status":"Guest"')"
expect "guest healthNotes görünür (oluşturan)" yes "$(has "$(body "$R")" 'diz')"
expect "update guest" 200 "$(put "/api/studio/students/$G1" "{\"fullName\":\"Ayse Guest2\",\"phone\":\"555111\",\"source\":\"Phone\"}" "$ATOK")"
expect "list students" 200 "$(gcode /api/studio/students "$ATOK")"
expect "search students" 200 "$(gcode "/api/studio/students/search?query=ayse" "$ATOK")"

echo "--- Misafir → User dönüşümü (yeni user) + Id korunumu ---"
R=$(post "/api/studio/students/$G1/convert-to-user" "{\"email\":\"g1new+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK")
expect "convert (new user)" 200 "$(code "$R")"
G1_AFTER=$(body "$R" | jget id)
expect "StudentProfile Id KORUNDU" "$G1" "$G1_AFTER"
expect "convert sonrası status=Active" yes "$(has "$(body "$R")" '"status":"Active"')"
expect "convert tekrar → 409" 409 "$(code "$(post "/api/studio/students/$G1/convert-to-user" "{\"email\":\"x+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK")")"

echo "--- Tenant izolasyonu (Owner C, A'nın öğrencisine erişemez) ---"
expect "C get A student → 404" 404 "$(gcode "/api/studio/students/$G1" "$CTOK")"
expect "C update A student → 404" 404 "$(put "/api/studio/students/$G1" "{\"fullName\":\"hack\",\"source\":\"Phone\"}" "$CTOK")"
expect "C deactivate A student → 404" 404 "$(code "$(post "/api/studio/students/$G1/deactivate" '{}' "$CTOK")")"

# v1.1.2 (privacy/role hardening): öğrenci yönetimi VE global öğrenci listesi/okuma yalnız Owner/StudioAdmin.
echo "--- Instructor öğrenci yönetimi + global liste/okuma erişemez ---"
expect "Instructor misafir oluştur → 403" 403 "$(code "$(post /api/studio/students/guest "{\"fullName\":\"I1 Guest\",\"source\":\"WalkIn\"}" "$I1TOK")")"
expect "Instructor öğrenci listesi (GET) → 403 (PII/HealthNotes; akış buna muhtaç değil)" 403 "$(gcode /api/studio/students "$I1TOK")"
expect "Instructor öğrenci arama (GET) → 403" 403 "$(gcode "/api/studio/students/search?query=x" "$I1TOK")"
expect "Instructor öğrenci by-id (GET) → 403" 403 "$(gcode "/api/studio/students/$G1" "$I1TOK")"

echo "--- Student erişim engeli + kendi profili ---"
STOK=$(login "g1new+$TS@pf.local")
expect "Student yönetim listesine 403" 403 "$(gcode /api/studio/students "$STOK")"
expect "Student /api/student/me → 200" 200 "$(gcode /api/student/me "$STOK")"

echo "--- Convert: mevcut User + duplicate koruması ---"
G2=$(body "$(post /api/studio/students/guest "{\"fullName\":\"G2\",\"source\":\"Referral\"}" "$ATOK")" | jget id)
expect "convert (existing user)" 200 "$(code "$(post "/api/studio/students/$G2/convert-to-user" "{\"email\":\"$EXIST\"}" "$ATOK")")"
G3=$(body "$(post /api/studio/students/guest "{\"fullName\":\"G3\",\"source\":\"Other\"}" "$ATOK")" | jget id)
expect "aynı user'a 2. aktif profil → 409" 409 "$(code "$(post "/api/studio/students/$G3/convert-to-user" "{\"email\":\"$EXIST\"}" "$ATOK")")"

echo "--- Pasifleştir / yeniden aktifleştir + pasif dönüştürülemez (Owner — öğrenci yönetimi) ---"
GD=$(body "$(post /api/studio/students/guest "{\"fullName\":\"GD\",\"source\":\"WalkIn\"}" "$ATOK")" | jget id)
expect "deactivate guest" 200 "$(code "$(post "/api/studio/students/$GD/deactivate" '{}' "$ATOK")")"
expect "pasif misafir convert → 409/400" 409 "$(code "$(post "/api/studio/students/$GD/convert-to-user" "{\"email\":\"z+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK")")"
expect "reactivate guest" 200 "$(code "$(post "/api/studio/students/$GD/reactivate" '{}' "$ATOK")")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 2 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 2 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
