#!/usr/bin/env bash
# Pliaforma — Sprint 31B-2 hesap kilitleme smoke.
# 5 başarısız login → kilit; kilit sırasında (doğru parola dahil) → 423; başarılı login sayacı sıfırlar.
# Kullanım: scripts/lockout-smoke.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; WRONG="Yanlis123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
reg(){ curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/register-studio" -H 'Content-Type: application/json' \
  -d "{\"studioName\":\"Lock $TS $1\",\"ownerFullName\":\"O\",\"email\":\"$2\",\"password\":\"$PW\"}"; }
login(){ curl -sk -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$1\",\"password\":\"$2\"}"; }

# --- Senaryo 1: başarılı login sayacı sıfırlar (4 yanlış < eşik, sonra doğru) ---
E1="lock1+$TS@pf.local"
expect "kayıt 1" 200 "$(reg a "$E1")"
for i in 1 2 3 4; do login "$E1" "$WRONG" >/dev/null; done
expect "4 yanlıştan sonra doğru login → 200 (henüz kilit yok)" 200 "$(login "$E1" "$PW")"
# sayaç sıfırlandıysa tekrar 4 yanlış kilide yetmez:
for i in 1 2 3 4; do login "$E1" "$WRONG" >/dev/null; done
expect "reset sonrası 4 yanlış → hâlâ kilit yok (doğru 200)" 200 "$(login "$E1" "$PW")"

# --- Senaryo 2: 5 yanlış → kilit; kilit sırasında doğru parola bile 423 ---
E2="lock2+$TS@pf.local"
expect "kayıt 2" 200 "$(reg b "$E2")"
C5=""
for i in 1 2 3 4 5; do C5=$(login "$E2" "$WRONG"); done
expect "5. yanlış denemenin yanıtı 401" 401 "$C5"
expect "6. deneme (yanlış) kilitli → 423" 423 "$(login "$E2" "$WRONG")"
expect "kilit sırasında DOĞRU parola bile → 423" 423 "$(login "$E2" "$PW")"

echo
[ "$FAILED" = "0" ] && { printf "\033[32mHESAP KİLİTLEME GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
