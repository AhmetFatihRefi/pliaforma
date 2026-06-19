#!/usr/bin/env bash
# Pliaforma — Sprint 7 smoke (DEPRECATED — Sprint 18b cutover).
# Paket/kredi modeli ana üründen çıkarıldı (dormant; DB drop YOK). Ders hakkı/booking/uygunluk artık
# SÖZLEŞME tabanlı (bkz. sprint18a-smoke + sprint18b-smoke). Bu smoke yalnız korunan minimumu kanıtlar:
#   - Paket CRUD endpoint'i hâlâ yanıt veriyor (dormant, kırılmadı).
#   - Yetkisiz erişim + tenant izolasyonu bozulmadı.
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
body(){ echo "$1" | sed '$d'; }
regstudio(){ body "$(post /api/auth/register-studio "{\"studioName\":\"$1\",\"ownerFullName\":\"O\",\"email\":\"$2\",\"password\":\"$PW\"}")" | jget accessToken; }

ATOK=$(regstudio "S7 $TS" "s7+$TS@pf.local")
BTOK=$(regstudio "S7B $TS" "s7b+$TS@pf.local")
PL=$(body "$(post /api/studio/package-plans "{\"name\":\"Dormant $TS\",\"creditCount\":8,\"price\":2400}" "$ATOK")" | jget id)

echo "--- Paket endpoint dormant + yetki/tenant (Sprint 18b: ana akış SÖZLEŞME) ---"
expect "owner paket planı oluştur (dormant endpoint çalışıyor)" yes "$([ -n "$PL" ] && echo yes || echo no)"
expect "owner package-plans list → 200" 200 "$(gcode /api/studio/package-plans "$ATOK")"
expect "token'sız package-plans → 401" 401 "$(gcode /api/studio/package-plans)"
expect "tenant B kendi listesini görür → 200" 200 "$(gcode /api/studio/package-plans "$BTOK")"
expect "B, A'nın planını GET → 404 (tenant izolasyonu korunuyor)" 404 "$(gcode "/api/studio/package-plans/$PL" "$BTOK")"

echo
echo "DEPRECATED: paket booking/uygunluk testleri sözleşmeye taşındı (sprint18a/18b-smoke)."
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 7 SMOKE (deprecated): GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 7 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
