#!/usr/bin/env bash
# Pliaforma — Sprint 31C-1 audit coverage smoke.
# 14 yeni olayı üretir, DB'de doğrular; PII/tutar/sağlık/token sızıntısı yok; target dolu; viewer RBAC.
# PG gerekir. Kullanım: scripts/audit-coverage-smoke.sh [BASE]   (ADMIN_EMAIL/ADMIN_PW env ile override)
set -u
BASE="${1:-http://localhost:5132}"
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@pliaforma.local}"; ADMIN_PW="${ADMIN_PW:-Admin!2345}"
HEALTH="SECRET_HEALTH_$TS"   # audit'te ASLA görünmemeli
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
patch(){ curl -s -o /dev/null -w "%{http_code}" -X PATCH "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
gc(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pc(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
[ -z "$PGC" ] && { echo "postgres yok (PG_CONTAINER ver)"; exit 1; }
q(){ docker exec "$PGC" psql -U postgres -d pliaforma -tAc "$1" 2>/dev/null | tr -d '\r'; }
verify(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }

OWN="ac_own+$TS@pf.local"; ADM="ac_adm+$TS@pf.local"; REJ="ac_rej+$TS@pf.local"
R=$(post /api/auth/register-studio "{\"studioName\":\"AC $TS\",\"ownerFullName\":\"O\",\"email\":\"$OWN\",\"password\":\"$PW\"}")
ATOK=$(body "$R" | jget accessToken); SID=$(body "$R" | jget studioId)

# --- Üyelik yaşam döngüsü: invite/accept/role/reject/remove ---
post /api/auth/register "{\"fullName\":\"Adm\",\"email\":\"$ADM\",\"password\":\"$PW\"}" >/dev/null; verify "$ADM"
post /api/auth/register "{\"fullName\":\"Rej\",\"email\":\"$REJ\",\"password\":\"$PW\"}" >/dev/null; verify "$REJ"
ADM_MEM=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$ADM\",\"role\":\"StudioAdmin\"}" "$ATOK")" | jget membershipId)  # MemberInvited
ADTOK=$(body "$(post /api/auth/login "{\"email\":\"$ADM\",\"password\":\"$PW\"}")" | jget accessToken)
post "/api/members/$ADM_MEM/accept" '{}' "$ADTOK" >/dev/null   # MembershipAccepted
patch "/api/studio/memberships/$ADM_MEM/role" '{"role":"Instructor"}' "$ATOK" >/dev/null   # RoleChanged (+ InstructorProfile)
REJ_MEM=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$REJ\",\"role\":\"Instructor\",\"rate\":1,\"rateType\":\"PerSession\"}" "$ATOK")" | jget membershipId)  # MemberInvited
RJTOK=$(body "$(post /api/auth/login "{\"email\":\"$REJ\",\"password\":\"$PW\"}")" | jget accessToken)
post "/api/members/$REJ_MEM/reject" '{}' "$RJTOK" >/dev/null   # MembershipRejected
INS_PID=$(curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | jget instructorProfileId)  # ADM artık Instructor

# --- Sözleşme + ödeme + okuma ---
CT_ID=$(body "$(post /api/class-types "{\"name\":\"AC Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":8,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":50}" "$ATOK")" | jget id)
STU=$(body "$(post /api/studio/students/guest "{\"fullName\":\"AC Student\",\"source\":\"WalkIn\",\"healthNotes\":\"$HEALTH\"}" "$ATOK")" | jget id)
CON=$(body "$(post "/api/studio/students/$STU/contracts" "{\"classTypeId\":\"$CT_ID\",\"instructorProfileId\":\"$INS_PID\",\"startDate\":\"2026-07-01\",\"endDate\":\"2026-12-01\",\"totalLessonCount\":10,\"makeupAllowance\":2,\"price\":1000,\"currency\":\"TRY\",\"isIndividualDefault\":false}" "$ATOK")" | jget id)  # ContractCreated
pc "/api/studio/contracts/$CON/amendments" "{\"kind\":\"ExtraLessons\",\"additionalLessonCount\":2,\"additionalPrice\":200,\"currency\":\"TRY\"}" "$ATOK" >/dev/null  # ContractAmended
LINE=$(curl -s "$BASE/api/studio/students/$STU/contract-lines" -H "Authorization: Bearer $ATOK" | jget id)
P1=$(body "$(post "/api/studio/students/$STU/payments" "{\"studentContractLineId\":\"$LINE\",\"amount\":100,\"dueDate\":\"2026-07-15\",\"currency\":\"TRY\"}" "$ATOK")" | jget id)  # PaymentCreated
curl -s -o /dev/null -X PUT "$BASE/api/studio/payments/$P1" -H 'Content-Type: application/json' -H "Authorization: Bearer $ATOK" -d '{"amount":120,"dueDate":"2026-07-20"}'  # PaymentUpdated
pc "/api/studio/payments/$P1/mark-paid" '{}' "$ATOK" >/dev/null   # PaymentMarkedPaid
P2=$(body "$(post "/api/studio/students/$STU/payments" "{\"studentContractLineId\":\"$LINE\",\"amount\":100,\"dueDate\":\"2026-08-15\",\"currency\":\"TRY\"}" "$ATOK")" | jget id)
pc "/api/studio/payments/$P2/cancel" '{}' "$ATOK" >/dev/null      # PaymentCancelled
pc "/api/studio/contracts/$CON/cancel" '{}' "$ATOK" >/dev/null    # ContractCancelled
gc "/api/studio/students" "$ATOK" >/dev/null                      # StudentDataListed + HealthNotesAccessed
gc "/api/studio/students/$STU" "$ATOK" >/dev/null                 # HealthNotesAccessed (tekil)
pc "/api/studio/memberships/$ADM_MEM/remove" '{}' "$ATOK" >/dev/null  # MemberRemoved
sleep 1

echo "== 14 olay DB'de (bu stüdyo) =="
for ev in RoleChanged MemberInvited MembershipAccepted MembershipRejected MemberRemoved \
          PaymentCreated PaymentUpdated PaymentMarkedPaid PaymentCancelled \
          ContractCreated ContractAmended ContractCancelled StudentDataListed HealthNotesAccessed; do
  N=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='$ev' AND \"StudioId\"='$SID';")
  [ "${N:-0}" -gt 0 ] 2>/dev/null && ok "olay: $ev (n=$N)" || bad "olay YOK: $ev"
done

echo "== Sızıntı yok (tutar/sağlık/token/ham e-posta) =="
LK=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\" LIKE '%$HEALTH%';")
[ "${LK:-1}" = "0" ] && ok "sağlık metni audit'te YOK" || bad "SAĞLIK metni sızdı (n=$LK)"
AM=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"StudioId\"='$SID' AND (\"Detail\" LIKE '%1000%' OR \"Detail\" LIKE '%200%');")
[ "${AM:-1}" = "0" ] && ok "tutar (1000/200) audit'te YOK" || bad "TUTAR sızdı (n=$AM)"
EM=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\" LIKE '%ac_own+$TS%' OR \"Detail\" LIKE '%ac_adm+$TS%' OR \"ActorEmailMasked\" LIKE '%ac_own+$TS%';")
[ "${EM:-1}" = "0" ] && ok "ham e-posta audit'te YOK" || bad "HAM e-posta sızdı (n=$EM)"
TK=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\" LIKE '%eyJ%' OR \"Detail\" LIKE '%Bearer %';")
[ "${TK:-1}" = "0" ] && ok "token/secret audit'te YOK" || bad "TOKEN sızdı (n=$TK)"

echo "== Target type/id dolu (yazma olayları) =="
for ev in RoleChanged MemberInvited MembershipAccepted MembershipRejected MemberRemoved \
          PaymentCreated PaymentUpdated PaymentMarkedPaid PaymentCancelled \
          ContractCreated ContractAmended ContractCancelled; do
  M=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='$ev' AND \"StudioId\"='$SID' AND (\"TargetType\" IS NULL OR \"TargetId\" IS NULL);")
  [ "${M:-1}" = "0" ] && ok "$ev target dolu" || bad "$ev target EKSİK (n=$M)"
done

echo "== Viewer RBAC =="
ADTOK2=$(body "$(post /api/auth/login "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PW\"}")" | jget accessToken)
expect "PlatformAdmin viewer → 200" 200 "$(gc "/api/platform/security-audit?pageSize=5" "$ADTOK2")"
expect "Owner viewer → 403" 403 "$(gc "/api/platform/security-audit" "$ATOK")"
INTOK=$(body "$(post /api/auth/login "{\"email\":\"$ADM\",\"password\":\"$PW\"}")" | jget accessToken)
expect "Instructor viewer → 403" 403 "$(gc "/api/platform/security-audit" "$INTOK")"
expect "Anonim viewer → 401" 401 "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/platform/security-audit")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mAUDIT COVERAGE: TÜM KONTROLLER GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mAUDIT COVERAGE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
