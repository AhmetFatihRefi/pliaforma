#!/usr/bin/env bash
# Pliaforma — Sprint 31B-2 parola politikası smoke.
# Zayıf/yaygın parolalar reddedilir (400), güçlü parola kabul edilir (200).
# Kullanım: scripts/password-policy-smoke.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }

# register-studio HTTP kodu (benzersiz e-posta ile)
reg(){ curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/register-studio" \
  -H 'Content-Type: application/json' \
  -d "{\"studioName\":\"PW $TS $RANDOM\",\"ownerFullName\":\"O\",\"email\":\"pw+$TS+$RANDOM@pf.local\",\"password\":\"$1\"}"; }
# reset-password HTTP kodu (token doğrulamadan ÖNCE parola validasyonu çalışır)
reset(){ curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/reset-password" \
  -H 'Content-Type: application/json' -d "{\"token\":\"x\",\"newPassword\":\"$1\"}"; }

echo "== Zayıf parolalar register → 400 =="
expect "kısa (8<10)"          400 "$(reg 'Ab1!cdef')"
expect "özel karakter yok"    400 "$(reg 'Parola1234')"
expect "büyük harf yok"       400 "$(reg 'parola123!')"
expect "rakam yok"            400 "$(reg 'Parolaabc!')"
expect "yaygın (Password1!)"  400 "$(reg 'Password1!')"

echo "== Güçlü parola register → 200 =="
expect "Parola123! kabul"     200 "$(reg 'Parola123!')"

echo "== Reset: zayıf parola → 400 (validasyon token'dan önce) =="
expect "reset zayıf (kısa)"   400 "$(reset 'Ab1!')"
expect "reset yaygın"         400 "$(reset 'Password1!')"

echo
[ "$FAILED" = "0" ] && { printf "\033[32mPAROLA POLİTİKASI GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
