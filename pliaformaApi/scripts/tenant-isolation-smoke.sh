#!/usr/bin/env bash
# Pliaforma — Sprint 31C-1 tenant izolasyon regresyonu.
# Studio A owner, Studio B owner+instructor+student+contract+payment. A token ile B nesnelerine cross-tenant
# erişim → 404/boş; switch-studio B → 403; (Mfa açıksa) mfa_pending cross-tenant → 403.
# PG gerekir (instructor e-posta doğrulaması). Kullanım: scripts/tenant-isolation-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gc(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pc(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
[ -z "$PGC" ] && { echo "postgres yok (PG_CONTAINER ver)"; exit 1; }
verify(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }

# ---- Studio A (owner) ----
OWN_A="ta_own+$TS@pf.local"
RA=$(post /api/auth/register-studio "{\"studioName\":\"TenantA $TS\",\"ownerFullName\":\"OA\",\"email\":\"$OWN_A\",\"password\":\"$PW\"}")
ATOK_A=$(body "$RA" | jget accessToken); SID_A=$(body "$RA" | jget studioId)

# ---- Studio B (owner + instructor + classType + student + contract + payment) ----
OWN_B="tb_own+$TS@pf.local"; INS_B="tb_ins+$TS@pf.local"
RB=$(post /api/auth/register-studio "{\"studioName\":\"TenantB $TS\",\"ownerFullName\":\"OB\",\"email\":\"$OWN_B\",\"password\":\"$PW\"}")
BTOK=$(body "$RB" | jget accessToken); SID_B=$(body "$RB" | jget studioId)
post /api/auth/register "{\"fullName\":\"InsB\",\"email\":\"$INS_B\",\"password\":\"$PW\"}" >/dev/null; verify "$INS_B"
INV=$(post /api/studio/memberships/invite "{\"email\":\"$INS_B\",\"role\":\"Instructor\",\"rate\":400,\"rateType\":\"PerSession\"}" "$BTOK")
INS_MEM=$(body "$INV" | jget membershipId)
INTOK=$(body "$(post /api/auth/login "{\"email\":\"$INS_B\",\"password\":\"$PW\"}")" | jget accessToken)
post "/api/members/$INS_MEM/accept" '{}' "$INTOK" >/dev/null
INS_PID=$(curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $BTOK" | jget instructorProfileId)
CT=$(post /api/class-types "{\"name\":\"TB Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":8,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":50}" "$BTOK")
CT_ID=$(body "$CT" | jget id)
ST=$(post /api/studio/students/guest "{\"fullName\":\"TB Student\",\"source\":\"WalkIn\",\"healthNotes\":\"diz\"}" "$BTOK")
B_STUDENT=$(body "$ST" | jget id)
CON=$(post "/api/studio/students/$B_STUDENT/contracts" "{\"classTypeId\":\"$CT_ID\",\"instructorProfileId\":\"$INS_PID\",\"startDate\":\"2026-07-01\",\"endDate\":\"2026-12-01\",\"totalLessonCount\":10,\"makeupAllowance\":2,\"price\":1000,\"currency\":\"TRY\",\"isIndividualDefault\":false}" "$BTOK")
B_CONTRACT=$(body "$CON" | jget id)
B_LINE=$(curl -s "$BASE/api/studio/students/$B_STUDENT/contract-lines" -H "Authorization: Bearer $BTOK" | jget id)
PAY=$(post "/api/studio/students/$B_STUDENT/payments" "{\"studentContractLineId\":\"$B_LINE\",\"amount\":100,\"dueDate\":\"2026-07-15\",\"currency\":\"TRY\"}" "$BTOK")
B_PAYMENT=$(body "$PAY" | jget id)

echo "== Seed kontrolü (B nesneleri oluştu) =="
[ -n "$B_STUDENT" ] && ok "B student" || bad "B student oluşmadı"
[ -n "$B_CONTRACT" ] && ok "B contract" || bad "B contract oluşmadı (instructorProfileId=$INS_PID, classType=$CT_ID)"
[ -n "$B_PAYMENT" ] && ok "B payment" || bad "B payment oluşmadı (line=$B_LINE)"

echo "== A token ile B nesnelerine cross-tenant ERİŞİM YOK (404) =="
expect "A → B student GET → 404" 404 "$(gc "/api/studio/students/$B_STUDENT" "$ATOK_A")"
expect "A → B contract GET → 404" 404 "$(gc "/api/studio/contracts/$B_CONTRACT" "$ATOK_A")"
expect "A → B student deactivate (write) → 404" 404 "$(pc "/api/studio/students/$B_STUDENT/deactivate" '{}' "$ATOK_A")"
expect "A → B contract cancel (write) → 404" 404 "$(pc "/api/studio/contracts/$B_CONTRACT/cancel" '{}' "$ATOK_A")"
expect "A → B payment cancel (write) → 404" 404 "$(pc "/api/studio/payments/$B_PAYMENT/cancel" '{}' "$ATOK_A")"
expect "A → B payment mark-paid (write) → 404" 404 "$(pc "/api/studio/payments/$B_PAYMENT/mark-paid" '{}' "$ATOK_A")"

echo "== A token ile B öğrencisinin liste uçları BOŞ (kendi stüdyosuna scope) =="
LC=$(curl -s "$BASE/api/studio/students/$B_STUDENT/contracts" -H "Authorization: Bearer $ATOK_A")
echo "$LC" | grep -q '"data":\[\]' && ok "A → B student contracts boş" || bad "A → B student contracts BOŞ DEĞİL (sızıntı!)"
LP=$(curl -s "$BASE/api/studio/students/$B_STUDENT/payments" -H "Authorization: Bearer $ATOK_A")
echo "$LP" | grep -q '"data":\[\]' && ok "A → B student payments boş" || bad "A → B student payments BOŞ DEĞİL (sızıntı!)"

echo "== switch-studio: A, üye OLMADIĞI B'ye geçemez → 403 =="
expect "A switch-studio B → 403" 403 "$(pc /api/auth/switch-studio "{\"studioId\":\"$SID_B\"}" "$ATOK_A")"

echo "== Sanity: B kendi öğrencisini görebilir (izolasyon fazla bloklamıyor) =="
expect "B → B student GET → 200" 200 "$(gc "/api/studio/students/$B_STUDENT" "$BTOK")"

echo "== mfa_pending token ile cross-tenant (yalnız Mfa açıkken; aksi halde atlanır) =="
MFAU="ta_mfa+$TS@pf.local"
RM=$(post /api/auth/register-studio "{\"studioName\":\"TM $TS\",\"ownerFullName\":\"M\",\"email\":\"$MFAU\",\"password\":\"$PW\"}")
if echo "$(body "$RM")" | grep -q '"mfaSetupRequired":true'; then
  MTOK=$(body "$RM" | jget accessToken)
  expect "mfa_pending → B student GET → 403" 403 "$(gc "/api/studio/students/$B_STUDENT" "$MTOK")"
else
  ok "mfa_pending kontrolü atlandı (Mfa kapalı; mfa-setup-smoke'ta doğrulanır)"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[36mTENANT ISOLATION: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mTENANT ISOLATION: BAŞARISIZ ❌\033[0m\n"; exit 1; }
