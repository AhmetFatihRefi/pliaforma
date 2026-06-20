#!/usr/bin/env bash
# Pliaforma — Sprint 1 smoke (hoca daveti/kabul, ClassType CRUD, atama, ROL + TENANT izolasyonu).
# Kullanım: scripts/sprint1-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s)
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
code(){ echo "$1" | tail -1; }
body(){ echo "$1" | sed '$d'; }

# --- Aktörler ---
AOWNER="owner-a+$TS@pf.local"; B="instr-b+$TS@pf.local"; COWNER="owner-c+$TS@pf.local"; PW="Parola123"

# Owner A + stüdyo A
R=$(post /api/auth/register-studio "{\"studioName\":\"Studio A $TS\",\"ownerFullName\":\"Owner A\",\"email\":\"$AOWNER\",\"password\":\"$PW\"}")
expect "register-studio A" 200 "$(code "$R")"; ATOK=$(body "$R" | jget accessToken)

# Kullanıcı B (davet edilecek hoca)
R=$(post /api/auth/register "{\"fullName\":\"Instructor B\",\"email\":\"$B\",\"password\":\"$PW\"}")
expect "register user B" 200 "$(code "$R")"

# Owner C + stüdyo C (tenant izolasyon karşı tarafı)
R=$(post /api/auth/register-studio "{\"studioName\":\"Studio C $TS\",\"ownerFullName\":\"Owner C\",\"email\":\"$COWNER\",\"password\":\"$PW\"}")
expect "register-studio C" 200 "$(code "$R")"; CTOK=$(body "$R" | jget accessToken)

echo "--- ClassType (Owner A) ---"
R=$(post /api/class-types "{\"name\":\"Reformer Group\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"Intermediate\",\"purpose\":\"General\",\"defaultDurationMinutes\":50,\"description\":null}" "$ATOK")
expect "create class-type" 200 "$(code "$R")"; CT_A=$(body "$R" | jget id)
# Sprint 28A: ders tipi sadeleşti → kapasite/seviye/amaç/ekipman kaldırıldı; fazla alanlar yok sayılır (validation yok) → 200.
R=$(post /api/class-types "{\"name\":\"Sade Grup\",\"capacityType\":\"Private\",\"maxCapacity\":3,\"defaultDurationMinutes\":50}" "$ATOK")
expect "ders tipi (fazla/kapasite alanları yok sayılır) → 200" 200 "$(code "$R")"
LC=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/class-types" -H "Authorization: Bearer $ATOK")
expect "list class-types" 200 "$LC"

echo "--- Hoca daveti / kabul ---"
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
R=$(post /api/studio/memberships/invite "{\"email\":\"$B\",\"role\":\"Instructor\",\"displayName\":\"Hoca B\",\"rate\":500,\"rateType\":\"PerSession\"}" "$ATOK")
expect "invite instructor" 200 "$(code "$R")"; MEMID=$(body "$R" | jget membershipId)
expect "invite anında InstructorProfile YOK (Sprint 17)" "" "$(body "$R" | jget instructorProfileId)"
# B giriş + davetleri + (doğrulanmamış accept 403) + doğrula + kabul → profil kabulde oluşur
R=$(post /api/auth/login "{\"email\":\"$B\",\"password\":\"$PW\"}"); BTOK=$(body "$R" | jget accessToken)
INVC=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/members/my-invitations" -H "Authorization: Bearer $BTOK")
expect "B my-invitations" 200 "$INVC"
expect "doğrulanmamış B accept → 403 (Sprint 17)" 403 "$(code "$(post "/api/members/$MEMID/accept" "{}" "$BTOK")")"
vrf "$B"
R=$(post "/api/members/$MEMID/accept" "{}" "$BTOK"); expect "B accept invitation" 200 "$(code "$R")"
PROF=$(ipid "$B")
expect "kabul sonrası InstructorProfile oluştu (Sprint 17)" yes "$([ -n "$PROF" ] && echo yes || echo no)"
# B yeniden giriş → A bağlamı otomatik
R=$(post /api/auth/login "{\"email\":\"$B\",\"password\":\"$PW\"}"); BTOK=$(body "$R" | jget accessToken)

echo "--- Hoca↔grup atama (Owner A) ---"
R=$(post "/api/instructors/$PROF/class-types" "{\"classTypeId\":\"$CT_A\",\"rateOverride\":600}" "$ATOK")
expect "assign instructor→class-type" 200 "$(code "$R")"
AC=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/instructors/$PROF/class-types" -H "Authorization: Bearer $ATOK")
expect "list instructor class-types" 200 "$AC"
RC=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/instructors/$PROF/rate" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d "{\"rate\":750,\"rateType\":\"PerSession\"}")
expect "update instructor rate" 200 "$RC"

echo "--- ROL kontrolü (Instructor yazamaz) ---"
R=$(post /api/class-types "{\"name\":\"X\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"Beginner\",\"purpose\":\"General\",\"defaultDurationMinutes\":50}" "$BTOK")
expect "instructor create class-type → 403" 403 "$(code "$R")"

echo "--- TENANT izolasyonu (Owner C, A'nın grubuna erişemez) ---"
GC=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/class-types/$CT_A" -H "Authorization: Bearer $CTOK")
expect "C get A's class-type → 404" 404 "$GC"
UC=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/class-types/$CT_A" -H 'Content-Type: application/json' -H "Authorization: Bearer $CTOK" -d "{\"name\":\"hack\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"Beginner\",\"purpose\":\"General\",\"defaultDurationMinutes\":50,\"isActive\":true}")
expect "C update A's class-type → 404" 404 "$UC"
DC=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/class-types/$CT_A" -H "Authorization: Bearer $CTOK")
expect "C delete A's class-type → 404" 404 "$DC"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 1 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 1 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
