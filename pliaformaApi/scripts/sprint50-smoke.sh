#!/usr/bin/env bash
# Pliaforma — Sprint 50 smoke (Trusted MFA Device / "bu cihazi hatirla").
# ON KOSUL: backend Mfa__Enabled=true + node (TOTP icin). Kullanim: scripts/sprint50-smoke.sh [BASE]
# Senaryolar:
#   1 Owner ilk login -> MFA challenge (tam token YOK).
#   2 verify + remember=UNCHECKED -> ayni cihazda sonraki login yine MFA ister (trusted cookie set EDILMEZ).
#   3 verify + remember=CHECKED  -> ayni cihazda (cookie) sonraki login MFA ISTEMEZ (tam token).
#   4 trusted cookie YOK (farkli/gizli cihaz)         -> MFA ister.
#   6 GECERSIZ trusted cookie                          -> MFA ister.
#   5 REVOKE (parola degisimi) -> trusted gecersiz     -> MFA ister.
#   7 Instructor (MFA gerekmez) -> challenge YOK, direkt tam token.
#   8 recovery code akisi bozulmadi (challenge -> recovery/verify 200; trusted cookie set edilmez).
# TOTP replay penceresi dar oldugundan her senaryo icin AYRI enrolled owner kullanilir.
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
TS=$(date +%s); PW="Parola123!"; NPW="YeniParola456!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
totp(){ node "$DIR/totp.mjs" "$1" "${2:-0}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT

post(){ curl -sk -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
# login: $1=body  $2=jar(read+write, ops)  $3=inline-cookie (ops, "name=value")
login(){ curl -sk -w "\n%{http_code}" -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' ${2:+-c "$2" -b "$2"} ${3:+-b "$3"} -d "$1"; }
# verify: $1=challenge $2=code $3=remember(true/false) $4=jar(ops)
verify(){ curl -sk -w "\n%{http_code}" -X POST "$BASE/api/auth/mfa/challenge/verify" -H 'Content-Type: application/json' ${4:+-c "$4" -b "$4"} -d "{\"challengeToken\":\"$1\",\"code\":\"$2\",\"rememberDevice\":$3}"; }
recov(){ curl -sk -w "\n%{http_code}" -X POST "$BASE/api/auth/mfa/recovery-code/verify" -H 'Content-Type: application/json' ${3:+-c "$3" -b "$3"} -d "{\"challengeToken\":\"$1\",\"recoveryCode\":\"$2\"}"; }
haschallenge(){ echo "$1" | grep -q '"mfaChallengeRequired":true'; }
hastoken(){ echo "$1" | grep -q '"accessToken"'; }
jarhastrusted(){ [ -f "$1" ] && grep -q "pliaforma_mfa_trusted" "$1"; }

# enroll <tag> -> globals: EM SEC ; recovery kodunu $TMP/rc yazar
enroll(){
  EM="own$1+$TS@pf.local"
  local at=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S50$1 $TS\",\"ownerFullName\":\"O\",\"email\":\"$EM\",\"password\":\"$PW\"}")" | jget accessToken)
  SEC=$(body "$(post /api/auth/mfa/setup/start "{}" "$at")" | jget manualKey)
  local sv=$(body "$(post /api/auth/mfa/setup/verify "{\"code\":\"$(totp "$SEC")\"}" "$at")")
  echo "$sv" | grep -o '[A-Z0-9]\{5\}-[A-Z0-9]\{5\}' | head -1 > "$TMP/rc"
}

echo "== Sprint 50: Trusted MFA Device =="
# On-kontrol: MFA gercekten acik mi? (register-studio owner'i MFA-pending olmali)
PRE=$(body "$(post /api/auth/register-studio "{\"studioName\":\"Pre $TS\",\"ownerFullName\":\"O\",\"email\":\"pre+$TS@pf.local\",\"password\":\"$PW\"}")")
echo "$PRE" | grep -q '"mfaSetupRequired":true' && ok "on-kontrol: MFA acik (owner mfaSetupRequired)" \
  || { bad "MFA KAPALI gorunuyor -> backend'i Mfa__Enabled=true ile calistir"; echo; echo "== ATLANDI (MFA kapali) =="; exit 2; }

echo "--- 1) Owner ilk login -> MFA challenge ---"
enroll a
LA=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}")")
haschallenge "$LA" && ok "1: ilk login mfaChallengeRequired=true" || bad "1: challenge yok"
hastoken "$LA" && bad "1: login full token dondu (MFA bypass!)" || ok "1: full token DONMEDI"

echo "--- 2) remember=UNCHECKED -> sonraki login yine MFA ---"
JB="$TMP/jarB"
CTB=$(echo "$LA" | jget challengeToken)   # 1'deki challenge'i kullan (ayni owner a)
VB=$(verify "$CTB" "$(totp "$SEC" 1)" false "$JB")
expect "2: verify (remember=false) -> 200" 200 "$(code "$VB")"
jarhastrusted "$JB" && bad "2: trusted cookie set EDILDI (olmamali)" || ok "2: trusted cookie set edilmedi"
L2=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}" "$JB")")
haschallenge "$L2" && ok "2: sonraki login yine MFA ister" || bad "2: MFA istenmedi (bypass!)"

echo "--- 3) remember=CHECKED -> ayni cihazda MFA atlanir ---"
enroll c; JC="$TMP/jarC"
LC=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}" "$JC")")
CTC=$(echo "$LC" | jget challengeToken)
VC=$(verify "$CTC" "$(totp "$SEC" 1)" true "$JC")
expect "3: verify (remember=true) -> 200" 200 "$(code "$VC")"
jarhastrusted "$JC" && ok "3: trusted cookie set edildi (pliaforma_mfa_trusted)" || bad "3: trusted cookie YOK"
L3=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}" "$JC")")
{ hastoken "$L3" && ! haschallenge "$L3"; } && ok "3: ayni cihazda sonraki login MFA ISTEMEDI (tam token)" || bad "3: MFA istendi (trusted calismadi)"

echo "--- 4) cookie YOK (farkli/gizli cihaz) -> MFA ister ---"
L4=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}")")   # jar yok
haschallenge "$L4" && ok "4: cookiesiz login MFA ister" || bad "4: cookiesiz login MFA istemedi (bypass!)"

echo "--- 6) GECERSIZ trusted cookie -> MFA ister ---"
L6=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}" "" "pliaforma_mfa_trusted=bogus-invalid-token-$TS")")
haschallenge "$L6" && ok "6: gecersiz cookie MFA ister" || bad "6: gecersiz cookie MFA istemedi (bypass!)"

echo "--- 5) REVOKE (parola degisimi) -> trusted gecersiz ---"
enroll e; JE="$TMP/jarE"
LE=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}" "$JE")")
CTE=$(echo "$LE" | jget challengeToken)
VE=$(body "$(verify "$CTE" "$(totp "$SEC" 1)" true "$JE")")
ATE=$(echo "$VE" | jget accessToken)
# trusted once calisiyor mu? (revoke oncesi)
L5a=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}" "$JE")")
hastoken "$L5a" && ! haschallenge "$L5a" && ok "5: revoke oncesi trusted calisiyor" || bad "5: revoke oncesi trusted calismadi"
# parola degistir (tum trusted cihazlar iptal)
expect "5: change-password -> 200" 200 "$(code "$(post /api/auth/change-password "{\"currentPassword\":\"$PW\",\"newPassword\":\"$NPW\"}" "$ATE")")"
L5b=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$NPW\"}" "$JE")")   # yeni parola + eski trusted cookie
haschallenge "$L5b" && ok "5: parola degisiminden sonra trusted gecersiz -> MFA ister" || bad "5: revoke sonrasi MFA istenmedi (bypass!)"

echo "--- 7) Instructor MFA gerekmez -> challenge yok ---"
IE="hoca50+$TS@pf.local"
OWA=$(body "$(post /api/auth/register-studio "{\"studioName\":\"Inv $TS\",\"ownerFullName\":\"O\",\"email\":\"ownerinv+$TS@pf.local\",\"password\":\"$PW\"}")")
# Owner MFA-pending token ile invite gonderebilir mi? invite icin normal owner token gerek; MFA acikken owner da pending.
# Basitlik: instructor'i register + owner davetiyle degil, DOGRUDAN register edip login'e bakiyoruz (uyeliksiz de MFA gerekmez).
post /api/auth/register "{\"fullName\":\"Hoca50\",\"email\":\"$IE\",\"password\":\"$PW\"}" >/dev/null
LI=$(body "$(login "{\"email\":\"$IE\",\"password\":\"$PW\"}")")
{ hastoken "$LI" && ! haschallenge "$LI"; } && ok "7: MFA-gerekmeyen kullanici challenge YOK (tam token)" || bad "7: MFA-gerekmeyen kullaniciya challenge cikti"

echo "--- 8) recovery code akisi bozulmadi ---"
enroll r; JR="$TMP/jarR"; RC=$(cat "$TMP/rc")
LR=$(body "$(login "{\"email\":\"$EM\",\"password\":\"$PW\"}")")
CTR=$(echo "$LR" | jget challengeToken)
[ -n "$RC" ] && ok "8: recovery kodu enroll'dan alindi ($RC)" || bad "8: recovery kodu alinamadi"
RV=$(recov "$CTR" "$RC" "$JR")
expect "8: recovery/verify -> 200" 200 "$(code "$RV")"
jarhastrusted "$JR" && bad "8: recovery trusted cookie set etti (olmamali)" || ok "8: recovery trusted cookie set etmedi"

echo
[ "$FAILED" = 0 ] && echo "== TUM TESTLER GECTI ==" || echo "== BAZI TESTLER BASARISIZ =="
exit $FAILED
