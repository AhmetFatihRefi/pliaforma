#!/usr/bin/env bash
# Pliaforma — Sprint 14 smoke (DEPRECATED — Sprint 18b cutover).
# Paket Planı V2 (ders grubu kapsamı, fiyat versiyonlama) + paket-uygunluk booking gate'i ana üründen çıkarıldı.
# Booking/ders hakkı artık SÖZLEŞME tabanlı (bkz. sprint18a-smoke + sprint18b-smoke). Paket modeli dormant
# (DB drop YOK). Bu smoke yalnız korunan minimumu kanıtlar:
#   - Paket planı V2 endpoint'i hâlâ yanıt veriyor (dormant, kırılmadı) — kapsam/fiyat alanları dâhil.
#   - Yetkisiz erişim + tenant izolasyonu bozulmadı.
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
body(){ echo "$1" | sed '$d'; }
regstudio(){ body "$(post /api/auth/register-studio "{\"studioName\":\"$1\",\"ownerFullName\":\"O\",\"email\":\"$2\",\"password\":\"$PW\"}")" | jget accessToken; }

ATOK=$(regstudio "S14 $TS" "s14+$TS@pf.local")
BTOK=$(regstudio "S14B $TS" "s14b+$TS@pf.local")
CT=$(body "$(post /api/class-types "{\"name\":\"Reformer $TS\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":6,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
# Paket Planı V2 (dormant): kapsam (allowedClassTypeIds) + fiyat
PLR=$(post /api/studio/package-plans "{\"name\":\"V2 Dormant $TS\",\"creditCount\":8,\"price\":2400,\"allowedClassTypeIds\":[\"$CT\"]}" "$ATOK")
PL=$(body "$PLR" | jget id)

echo "--- Paket Planı V2 dormant + yetki/tenant (Sprint 18b: ana akış SÖZLEŞME) ---"
expect "owner V2 paket planı oluştur (kapsamlı, dormant endpoint çalışıyor)" yes "$([ -n "$PL" ] && echo yes || echo no)"
expect "plan fiyat alanı döner (V2 şema korunuyor)" yes "$(has "$(body "$PLR")" '"price"')"
expect "owner package-plans list → 200" 200 "$(gcode /api/studio/package-plans "$ATOK")"
expect "token'sız package-plans → 401" 401 "$(gcode /api/studio/package-plans)"
expect "B, A'nın planını GET → 404 (tenant izolasyonu korunuyor)" 404 "$(gcode "/api/studio/package-plans/$PL" "$BTOK")"

echo
echo "DEPRECATED: paket-uygunluk booking gate + fiyat versiyonlama testleri sözleşmeye taşındı (sprint18a/18b-smoke)."
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 14 SMOKE (deprecated): GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 14 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
