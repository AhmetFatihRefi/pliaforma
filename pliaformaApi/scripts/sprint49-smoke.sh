#!/usr/bin/env bash
# Pliaforma — Sprint 49 smoke (Single Pilot Deployment Prep: public studio registration lockdown).
#   Flag: Registration__StudioRegistrationEnabled (kod varsayilani true; pilot compose'da false).
#   Script /api/auth/registration-status'u okuyup MODA gore dallanir:
#     ON  (true) : status=true; register-studio 200 (yeni studio acilir); login akisi calisir.
#     OFF (false): status=false; register-studio 403 (dishariya kapali); AMA
#                  - plain /register (stüdyosuz hesap) 200 (davet-kabul icin acik kalir)
#                  - login (o hesapla) 200 (mevcut kullanici akisi ETKILENMEZ)
#   Not: OFF modunu test etmek icin stack'i Registration__StudioRegistrationEnabled=false ile ayaga kaldir.
#   ASCII-only. Kullanim: scripts/sprint49-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"; PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jbool(){ echo "$1" | grep -o "\"$2\":\(true\|false\)" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -d "${2:-{\}}"; }
gbody(){ curl -s "$BASE$1"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
TS=$(date +%s)

echo "== Sprint 49: Single Pilot Deployment Prep (registration lockdown) =="

# 0) registration-status endpoint (anonim) 200 + boolean dondurur.
RS=$(gbody /api/auth/registration-status)
FLAG=$(jbool "$RS" studioRegistrationEnabled)
[ -n "$FLAG" ] && ok "registration-status -> studioRegistrationEnabled=$FLAG" || { bad "registration-status bool donmedi: $RS"; echo; echo "== BASARISIZ =="; exit 1; }
echo "-- mod: studioRegistrationEnabled = $FLAG --"

OWNER="o49+$TS@pf.local"
SBODY="{\"studioName\":\"S49 $TS\",\"ownerFullName\":\"O49\",\"email\":\"$OWNER\",\"password\":\"$PW\"}"

if [ "$FLAG" = "true" ]; then
  echo "--- ON: herkese acik kayit (tam SaaS/dev) ---"
  RC=$(code "$(post /api/auth/register-studio "$SBODY")")
  expect "register-studio (kayit acik) -> 200" 200 "$RC"
else
  echo "--- OFF: tek-salon pilot (kayit kapali) ---"
  RC=$(code "$(post /api/auth/register-studio "$SBODY")")
  expect "register-studio (kayit kapali) -> 403" 403 "$RC"

  # register (stüdyosuz hesap) ve login flag'ten ETKILENMEZ.
  ACC="acc49+$TS@pf.local"
  REGC=$(code "$(post /api/auth/register "{\"fullName\":\"Acc49\",\"email\":\"$ACC\",\"password\":\"$PW\"}")")
  expect "register (stüdyosuz hesap, kayit kapaliyken) -> 200" 200 "$REGC"
  LGC=$(code "$(post /api/auth/login "{\"email\":\"$ACC\",\"password\":\"$PW\"}")")
  expect "login (mevcut kullanici, kayit kapaliyken) -> 200" 200 "$LGC"
fi

echo
[ "$FAILED" = 0 ] && echo "== TUM TESTLER GECTI (mod: registration=$FLAG) ==" || echo "== BAZI TESTLER BASARISIZ (mod: registration=$FLAG) =="
exit $FAILED
