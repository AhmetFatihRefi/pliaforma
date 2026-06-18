#!/usr/bin/env bash
# Pliaforma — Sprint 15 smoke (DEPRECATED akış guard'ı — Sprint 17 Rev3 invite-only onboarding).
# JoinCode ile self-service stüdyoya katılım KALDIRILDI: /api/members/apply artık HER DURUMDA 410 döner;
# ne öğrenci ne hoca kodla başvurur. Bu smoke yalnız "self-apply kapalı" regresyon guard'ıdır.
# (Stüdyoya katılım yalnız davet ile — bkz. sprint17-smoke.) Postgres container gerekli (e-posta verify).
# Kullanım: scripts/sprint15-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (e-posta verify gerekli)."; exit 2; }
vrf(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1; }

# Stüdyo (JoinCode üretim endpoint'i geriye-uyum için durur) + doğrulanmış + doğrulanmamış kullanıcı.
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S15 $TS\",\"ownerFullName\":\"O\",\"email\":\"s15o+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
JCA=$(curl -s "$BASE/api/studio/join-code" -H "Authorization: Bearer $ATOK" | jget joinCode)
UE="s15u+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"U\",\"email\":\"$UE\",\"password\":\"$PW\"}" >/dev/null
UVTOK=$(login "$UE")   # doğrulanmamış token
vrf "$UE"; UTOK=$(login "$UE")   # doğrulanmış token

echo "--- Self-apply tamamen KAPALI (Rev3 invite-only) ---"
expect "Student JoinCode apply → 410" 410 "$(pcode /api/members/apply "{\"joinCode\":\"$JCA\",\"role\":\"Student\"}" "$UTOK")"
expect "Instructor JoinCode apply → 410" 410 "$(pcode /api/members/apply "{\"joinCode\":\"$JCA\",\"role\":\"Instructor\"}" "$UTOK")"
expect "geçersiz kod ile apply yine 410 (kayıt/varlık sızdırmaz)" 410 "$(pcode /api/members/apply "{\"joinCode\":\"ZZZZZZZZZZ\",\"role\":\"Student\"}" "$UTOK")"
expect "doğrulanmamış kullanıcı için de apply 410" 410 "$(pcode /api/members/apply "{\"joinCode\":\"$JCA\",\"role\":\"Student\"}" "$UVTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 15 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 15 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
