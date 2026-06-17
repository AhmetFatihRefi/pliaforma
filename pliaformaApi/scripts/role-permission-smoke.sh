#!/usr/bin/env bash
# Pliaforma — Rol & yetki smoke (Sprint 1 Fix).
# StudioAdmin yetkileri, Owner koruması, Instructor/Student erişim engeli.
# Kullanım: scripts/role-permission-smoke.sh [BASE]   (varsayılan http://localhost:5132)
# Not: Student üyeliği Sprint 1'de API'den oluşturulamaz → test için DB'ye doğrudan eklenir
#      (docker container: pliaforma-postgres / db: pliaforma).
set -u
BASE="${1:-http://localhost:5132}"
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "$2"; }
patch(){ curl -s -o /dev/null -w "%{http_code}" -X PATCH "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }

OWN="own+$TS@pf.local"; ADM="adm+$TS@pf.local"; INS="ins+$TS@pf.local"; STU="stu+$TS@pf.local"

# Aktörler
R=$(post /api/auth/register-studio "{\"studioName\":\"RoleStudio $TS\",\"ownerFullName\":\"Owner\",\"email\":\"$OWN\",\"password\":\"$PW\"}")
expect "register-studio (Owner)" 200 "$(code "$R")"
ATOK=$(body "$R" | jget accessToken); OWNER_MEM=$(body "$R" | jget membershipId); SID=$(body "$R" | jget studioId)
post /api/auth/register "{\"fullName\":\"Admin U\",\"email\":\"$ADM\",\"password\":\"$PW\"}" >/dev/null
post /api/auth/register "{\"fullName\":\"Instr U\",\"email\":\"$INS\",\"password\":\"$PW\"}" >/dev/null
R=$(post /api/auth/register "{\"fullName\":\"Stud U\",\"email\":\"$STU\",\"password\":\"$PW\"}"); STU_ID=$(body "$R" | jget id)

echo "--- StudioAdmin akışı ---"
R=$(post /api/studio/memberships/invite "{\"email\":\"$ADM\",\"role\":\"StudioAdmin\"}" "$ATOK")
expect "Owner invites StudioAdmin" 200 "$(code "$R")"; ADM_MEM=$(body "$R" | jget membershipId)
ADTOK=$(body "$(post /api/auth/login "{\"email\":\"$ADM\",\"password\":\"$PW\"}")" | jget accessToken)
expect "StudioAdmin accept" 200 "$(code "$(post "/api/members/$ADM_MEM/accept" '{}' "$ADTOK")")"
ADTOK=$(body "$(post /api/auth/login "{\"email\":\"$ADM\",\"password\":\"$PW\"}")" | jget accessToken)
R=$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":8,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":50}" "$ADTOK")
expect "StudioAdmin creates ClassType" 200 "$(code "$R")"
R=$(post /api/studio/memberships/invite "{\"email\":\"$INS\",\"role\":\"Instructor\",\"rate\":400,\"rateType\":\"PerSession\"}" "$ADTOK")
expect "StudioAdmin invites Instructor" 200 "$(code "$R")"; INS_MEM=$(body "$R" | jget membershipId)

echo "--- Owner koruması ---"
expect "StudioAdmin remove Owner → 409" 409 "$(code "$(post "/api/studio/memberships/$OWNER_MEM/remove" '{}' "$ADTOK")")"
expect "StudioAdmin change Owner role → 403" 403 "$(patch "/api/studio/memberships/$OWNER_MEM/role" '{"role":"Instructor"}' "$ADTOK")"
expect "Owner change own role → 409" 409 "$(patch "/api/studio/memberships/$OWNER_MEM/role" '{"role":"Instructor"}' "$ATOK")"
expect "Owner change StudioAdmin→Instructor → 200" 200 "$(patch "/api/studio/memberships/$ADM_MEM/role" '{"role":"Instructor"}' "$ATOK")"
expect "Owner change back →StudioAdmin → 200" 200 "$(patch "/api/studio/memberships/$ADM_MEM/role" '{"role":"StudioAdmin"}' "$ATOK")"

echo "--- Instructor erişim engeli ---"
INTOK=$(body "$(post /api/auth/login "{\"email\":\"$INS\",\"password\":\"$PW\"}")" | jget accessToken)
post "/api/members/$INS_MEM/accept" '{}' "$INTOK" >/dev/null
INTOK=$(body "$(post /api/auth/login "{\"email\":\"$INS\",\"password\":\"$PW\"}")" | jget accessToken)
expect "Instructor create ClassType → 403" 403 "$(code "$(post /api/class-types "{\"name\":\"X\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"Beginner\",\"purpose\":\"General\",\"defaultDurationMinutes\":50}" "$INTOK")")"
expect "Instructor list members → 403" 403 "$(gcode /api/studio/memberships "$INTOK")"
expect "Instructor invite → 403" 403 "$(code "$(post /api/studio/memberships/invite "{\"email\":\"$STU\",\"role\":\"Instructor\",\"rate\":1}" "$INTOK")")"

echo "--- Student erişim engeli (üyelik DB'den arrange edilir) ---"
SQL="INSERT INTO \"StudioMemberships\" (\"Id\",\"StudioId\",\"UserId\",\"Role\",\"Status\",\"Direction\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$SID','$STU_ID','Student','Active','InvitedByStudio', now() at time zone 'utc', false);"
if docker exec "$PGC" psql -U postgres -d pliaforma -c "$SQL" >/dev/null 2>&1; then
  STTOK=$(body "$(post /api/auth/login "{\"email\":\"$STU\",\"password\":\"$PW\"}")" | jget accessToken)
  expect "Student create ClassType → 403" 403 "$(code "$(post /api/class-types "{\"name\":\"X\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"Beginner\",\"purpose\":\"General\",\"defaultDurationMinutes\":50}" "$STTOK")")"
  expect "Student list members → 403" 403 "$(gcode /api/studio/memberships "$STTOK")"
  expect "Student invite → 403" 403 "$(code "$(post /api/studio/memberships/invite "{\"email\":\"$OWN\",\"role\":\"Instructor\",\"rate\":1}" "$STTOK")")"
else
  bad "Student arrange (DB insert başarısız — PG_CONTAINER=$PGC erişilemedi)"
fi

echo "--- StudioAdmin, Instructor'ı yönetebilir ---"
expect "StudioAdmin remove Instructor → 200" 200 "$(code "$(post "/api/studio/memberships/$INS_MEM/remove" '{}' "$ADTOK")")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mROLE-PERMISSION SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mROLE-PERMISSION SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
