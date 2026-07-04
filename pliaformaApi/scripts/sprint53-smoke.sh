#!/usr/bin/env bash
# Pliaforma — Sprint 53 mobil auth temeli smoke (fix pass ile genişletildi).
# Kapsam:
#   - Mobil body taşıması: refresh/trusted token body'de, Set-Cookie YOK, Cache-Control: no-store + Pragma.
#   - Ömür assert'leri: web refresh ~7 gün (cookie epoch), mobil refresh ~45 gün (body).
#   - Kanal bağlama: web token mobil uçta 401, mobil token web uçta 401, body'den client SPOOF edilemez.
#   - Atomik rotasyon (F5): aynı token'la eşzamanlı çift refresh → tam olarak BİR 200 + BİR 401.
#   - Reuse karar ağacı (F2/F3): rotasyonla tüketilmiş (zincirli) token grace İÇİ → 401 + audit(grace=true),
#     aile KORUNUR; grace DIŞI → 401 + AİLE İPTALİ + audit(grace=false). Logout/toplu iptal (zincirsiz) →
#     sessiz 401, aile iptali ve alarm YOK. Web kanalında da grace-dışı reuse tespiti doğrulanır.
#   - F1: tam yetkili bearer mobile mfa/setup/verify'da 403; mfa_pending token başarılı akış.
#   - MFA challenge + rememberDevice trusted token body; recovery-code trusted device ÜRETMEZ; mfa_pending allowlist.
#   - Audit assert'leri (dev DB'ye docker ile erişilebiliyorsa): RefreshTokenReuseDetected satırları oluşur,
#     Detail maskelidir (ham token/uzun hash YOK). DB erişimi yoksa bu kontroller SKIP yazar (FAIL değil).
# ÖN KOŞUL: backend Mfa__Enabled=true (mfa-login-smoke ile aynı). Kullanım: scripts/sprint53-smoke.sh [BASE]
# NOT: grace-dışı senaryolar tek ortak 65 sn beklemeyle test edilir → toplam süre ~2,5 dk.
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
TS=$(date +%s); PW="Parola123!"; FAILED=0
HDRS=$(mktemp); JAR=$(mktemp); JAR3=$(mktemp); R1=$(mktemp); R2=$(mktemp)
trap 'rm -f "$HDRS" "$JAR" "$JAR3" "$R1" "$R2"' EXIT

ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
skip(){ printf "  \033[33mSKIP\033[0m %s\n" "$1"; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
# Sayısal JSON alanı (Sprint 54 K6: epoch-seconds alanları string değil number'dır).
jgetn(){ grep -o "\"$1\":[0-9]*" | head -1 | sed "s/\"$1\"://"; }
totp(){ node "$DIR/totp.mjs" "$1" "${2:-0}"; }
post(){ curl -sk -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
posth(){ curl -sk -D "$HDRS" -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
hdr_has(){ grep -qi "$1" "$HDRS"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }

no_cookie(){ hdr_has "^set-cookie" && bad "$1: Set-Cookie VAR (mobil uç cookie yazamaz)" || ok "$1: Set-Cookie yok"; }
no_store(){ hdr_has "cache-control: *no-store" && ok "$1: Cache-Control no-store" || bad "$1: no-store YOK"
            hdr_has "pragma: *no-cache" && ok "$1: Pragma no-cache" || bad "$1: Pragma no-cache YOK"; }

# --- Audit DB yardımcıları (opsiyonel; dev compose'daki postgres container'ına docker ile bakar) ---
DBC="${AUDIT_DB_CONTAINER:-pliaforma-postgres}"
DBU="${AUDIT_DB_USER:-postgres}"; DBN="${AUDIT_DB_NAME:-pliaforma}"
audit_q(){ docker exec "$DBC" psql -U "$DBU" -d "$DBN" -At -c "$1" 2>/dev/null; }
AUDIT_ON=0; [ "$(audit_q 'SELECT 1')" = "1" ] && AUDIT_ON=1
reuse_count(){ audit_q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='RefreshTokenReuseDetected' AND \"Detail\" LIKE '%$1%'"; }
[ "$AUDIT_ON" = "1" ] && ok "audit DB erişimi var ($DBC) — audit assert'leri AKTİF" || skip "audit DB erişilemedi — audit assert'leri atlanacak"

days_until(){ echo $(( ($(date -d "$1" +%s) - $(date +%s)) / 86400 )); }

echo "== 1) Mobil taşıma: body token + cookie yok + no-store + ÖMÜR (~45g) =="
EMAIL="m53user+$TS@pf.local"
expect "web register → 200" 200 "$(code "$(post /api/auth/register "{\"fullName\":\"M53 User\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}")")"

ML=$(posth /api/auth/mobile/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
expect "mobil login → 200" 200 "$(code "$ML")"
MRT=$(body "$ML" | jget refreshToken)
[ -n "$MRT" ] && ok "mobil login: refreshToken BODY'de" || bad "mobil login: refreshToken body'de YOK"
no_cookie "mobil login"; no_store "mobil login"
MEXP=$(body "$ML" | jget refreshTokenExpiresAt); MD=$(days_until "$MEXP")
{ [ "$MD" -ge 43 ] && [ "$MD" -le 46 ]; } && ok "mobil refresh ömrü ~45 gün ($MD g)" || bad "mobil refresh ömrü beklenmedik: $MD gün ($MEXP)"
# Sprint 54 K6: makine-tüketimi epoch alanları (mobil istemci YALNIZ bunları kullanır).
RUNIX=$(body "$ML" | jgetn refreshTokenExpiresAtUnix); AUNIX=$(body "$ML" | jgetn accessTokenExpiresAtUnix)
NOW=$(date +%s)
{ [ -n "$RUNIX" ] && [ $(( (RUNIX - NOW) / 86400 )) -ge 43 ] && [ $(( (RUNIX - NOW) / 86400 )) -le 46 ]; } \
  && ok "K6: refreshTokenExpiresAtUnix ~45 gün (epoch)" || bad "K6: refreshTokenExpiresAtUnix eksik/yanlış ($RUNIX)"
{ [ -n "$AUNIX" ] && [ $(( (AUNIX - NOW) / 60 )) -ge 10 ] && [ $(( (AUNIX - NOW) / 60 )) -le 20 ]; } \
  && ok "K6: accessTokenExpiresAtUnix ~15 dk (epoch)" || bad "K6: accessTokenExpiresAtUnix eksik/yanlış ($AUNIX)"

echo "== 2) Rotasyon + grace İÇİ reuse: 401, aile KORUNUR, audit(grace=true) =="
MR=$(posth /api/auth/mobile/refresh "{\"refreshToken\":\"$MRT\"}")
expect "mobil refresh → 200" 200 "$(code "$MR")"
MRT2=$(body "$MR" | jget refreshToken)
{ [ -n "$MRT2" ] && [ "$MRT2" != "$MRT" ]; } && ok "rotasyon: yeni refreshToken (eskisinden farklı)" || bad "rotasyon başarısız"
no_cookie "mobil refresh"; no_store "mobil refresh"

GT_BEFORE=$([ "$AUDIT_ON" = "1" ] && reuse_count "grace=true" || echo 0)
expect "grace içi reuse (rotasyonlanan token) → 401" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$MRT\"}")")"
MR3=$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$MRT2\"}")
expect "grace içi: aile KORUNDU (güncel token çalışıyor)" 200 "$(code "$MR3")"
MRT3=$(body "$MR3" | jget refreshToken)
if [ "$AUDIT_ON" = "1" ]; then
  GT_AFTER=$(reuse_count "grace=true")
  [ "$GT_AFTER" -gt "$GT_BEFORE" ] && ok "grace içi reuse AUDIT yazdı (grace=true satırı +$((GT_AFTER-GT_BEFORE)))" || bad "grace içi reuse audit YAZMADI"
else skip "grace=true audit asserti (DB yok)"; fi

echo "== 3) Kanal bağlama + web regresyonu + web ÖMÜR (~7g) + client SPOOF =="
WL=$(curl -sk -c "$JAR" -D "$HDRS" -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")
echo "$WL" | grep -q '"refreshToken"' && bad "web login: refreshToken BODY'de (SIZINTI!)" || ok "web login: refreshToken body'de DEĞİL"
hdr_has "^set-cookie: *pf_refresh" && ok "web login: pf_refresh cookie yazıldı (mevcut davranış)" || bad "web login: pf_refresh cookie YOK"
WRT=$(awk '$6 == "pf_refresh" {print $7}' "$JAR" | tail -1)
WEXP=$(awk '$6 == "pf_refresh" {print $5}' "$JAR" | tail -1)
[ -n "$WRT" ] && ok "web cookie ham değeri alındı (test amaçlı)" || bad "web cookie değeri okunamadı"
WD=$(( (WEXP - $(date +%s)) / 86400 ))
{ [ "$WD" -ge 6 ] && [ "$WD" -le 8 ]; } && ok "web refresh ömrü ~7 gün ($WD g)" || bad "web refresh ömrü beklenmedik: $WD gün"

expect "web token'ı MOBİL uçta → 401 (kanal bağlama)" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$WRT\"}")")"
expect "web token + body'de client:Web SPOOF → yine 401" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$WRT\",\"client\":\"Web\"}")")"
expect "mobil token WEB uçta → 401 (kanal bağlama)" 401 "$(curl -sk -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -H "Cookie: pf_refresh=$MRT3" -d '{}')"
expect "web refresh (cookie ile) → 200 (regresyon)" 200 "$(curl -sk -b "$JAR" -c "$JAR" -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -d '{}')"

echo "== 4) F5: eşzamanlı çift refresh → tam olarak BİR 200 + BİR 401 =="
post /api/auth/mobile/refresh "{\"refreshToken\":\"$MRT3\"}" > "$R1" &
post /api/auth/mobile/refresh "{\"refreshToken\":\"$MRT3\"}" > "$R2" &
wait
C1=$(tail -1 "$R1"); C2=$(tail -1 "$R2")
{ [ "$C1$C2" = "200401" ] || [ "$C1$C2" = "401200" ]; } && ok "çift refresh: tek 200 + tek 401 ($C1/$C2 — double-spend yok)" || bad "çift refresh beklenmedik: $C1/$C2"
if [ "$C1" = "200" ]; then MRT4=$(sed '$d' "$R1" | jget refreshToken); else MRT4=$(sed '$d' "$R2" | jget refreshToken); fi
[ -n "$MRT4" ] && ok "kazanan istekten yeni token alındı" || bad "kazanan token alınamadı"

echo "== 5) Mobil logout → F2: zincirsiz revoked token SESSİZ 401 (alarm yok) =="
TOT_BEFORE=$([ "$AUDIT_ON" = "1" ] && reuse_count "" || echo 0)
LO=$(posth /api/auth/mobile/logout "{\"refreshToken\":\"$MRT4\"}")
expect "mobil logout → 200" 200 "$(code "$LO")"
no_cookie "mobil logout"
expect "logout sonrası refresh → 401" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$MRT4\"}")")"
if [ "$AUDIT_ON" = "1" ]; then
  TOT_AFTER=$(reuse_count "")
  [ "$TOT_AFTER" = "$TOT_BEFORE" ] && ok "logout-replay ALARM üretmedi (zincirsiz → sessiz 401)" || bad "logout-replay audit üretti (yanlış-pozitif!)"
else skip "logout-replay audit asserti (DB yok)"; fi

echo "== 6) Grace DIŞI senaryo hazırlığı (U2 mobil zincir, U3 web zincir, U4 logout-bayat) =="
E2="m53b+$TS@pf.local"; E3="m53c+$TS@pf.local"; E4="m53d+$TS@pf.local"
post /api/auth/register "{\"fullName\":\"M53 B\",\"email\":\"$E2\",\"password\":\"$PW\"}" >/dev/null
post /api/auth/register "{\"fullName\":\"M53 C\",\"email\":\"$E3\",\"password\":\"$PW\"}" >/dev/null
post /api/auth/register "{\"fullName\":\"M53 D\",\"email\":\"$E4\",\"password\":\"$PW\"}" >/dev/null
# U2: mobil zincir — A rotasyonla tüketilir (gerçek hırsızlık sinyali simülasyonu).
A=$(body "$(post /api/auth/mobile/login "{\"email\":\"$E2\",\"password\":\"$PW\"}")" | jget refreshToken)
B=$(body "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$A\"}")" | jget refreshToken)
# U3: web zincir — W1 rotasyonla tüketilir (web kanalında reuse tespiti).
curl -sk -c "$JAR3" -o /dev/null -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$E3\",\"password\":\"$PW\"}"
W1=$(awk '$6 == "pf_refresh" {print $7}' "$JAR3" | tail -1)
curl -sk -b "$JAR3" -c "$JAR3" -o /dev/null -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -d '{}'
# U4: iki oturum (C, D); C logout ile ZİNCİRSİZ revoke edilir — F2 yanlış-pozitif testi.
C=$(body "$(post /api/auth/mobile/login "{\"email\":\"$E4\",\"password\":\"$PW\"}")" | jget refreshToken)
D=$(body "$(post /api/auth/mobile/login "{\"email\":\"$E4\",\"password\":\"$PW\"}")" | jget refreshToken)
post /api/auth/mobile/logout "{\"refreshToken\":\"$C\"}" >/dev/null
echo "  (grace=60 sn — 65 sn bekleniyor...)"
sleep 65

echo "-- F2: U4 zincirsiz bayat token (logout) grace DIŞI → sessiz 401, AİLE KORUNUR --"
TOT_BEFORE=$([ "$AUDIT_ON" = "1" ] && reuse_count "" || echo 0)
expect "U4: logout'lu token replay → 401" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$C\"}")")"
expect "U4: diğer oturum (D) HÂLÂ çalışıyor (aile iptal edilmedi)" 200 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$D\"}")")"
if [ "$AUDIT_ON" = "1" ]; then
  TOT_AFTER=$(reuse_count "")
  [ "$TOT_AFTER" = "$TOT_BEFORE" ] && ok "U4: alarm YOK (zincirsiz → RefreshTokenReuseDetected yazılmadı)" || bad "U4: yanlış-pozitif alarm yazıldı"
else skip "U4 audit asserti (DB yok)"; fi

echo "-- U2: MOBİL zincirli token grace DIŞI reuse → aile iptali + audit(grace=false) --"
GF_BEFORE=$([ "$AUDIT_ON" = "1" ] && reuse_count "grace=false" || echo 0)
expect "U2: rotasyonlanmış token replay → 401" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$A\"}")")"
expect "U2: aile iptali — güncel token (B) DA 401" 401 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"$B\"}")")"
expect "U2: aile iptali sonrası yeniden login → 200" 200 "$(code "$(post /api/auth/mobile/login "{\"email\":\"$E2\",\"password\":\"$PW\"}")")"
if [ "$AUDIT_ON" = "1" ]; then
  GF_AFTER=$(reuse_count "grace=false")
  [ "$GF_AFTER" -gt "$GF_BEFORE" ] && ok "U2: audit(grace=false) yazıldı" || bad "U2: grace=false audit YOK"
else skip "U2 audit asserti (DB yok)"; fi

echo "-- U3: WEB kanalında zincirli token grace DIŞI reuse → aile iptali --"
expect "U3: eski web cookie token replay → 401" 401 "$(curl -sk -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -H "Cookie: pf_refresh=$W1" -d '{}')"
expect "U3: güncel web cookie DA öldü (aile iptali web'de de çalışıyor)" 401 "$(curl -sk -b "$JAR3" -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/refresh" -H 'Content-Type: application/json' -d '{}')"

echo "== 7) MFA: F1 gate + mobil setup + challenge + trusted device (body) =="
# F1: tam yetkili (MFA'sız U1) bearer mobile setup verify KULLANAMAZ.
FULL_AT=$(body "$(post /api/auth/mobile/login "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}")" | jget accessToken)
expect "F1: tam yetkili bearer → mobile mfa/setup/verify → 403" 403 "$(code "$(post /api/auth/mobile/mfa/setup/verify "{\"code\":\"000000\"}" "$FULL_AT")")"

OEMAIL="m53owner+$TS@pf.local"
RS=$(post /api/auth/register-studio "{\"studioName\":\"M53 $TS\",\"ownerFullName\":\"O\",\"email\":\"$OEMAIL\",\"password\":\"$PW\"}")
expect "register-studio → 200" 200 "$(code "$RS")"
AT=$(body "$RS" | jget accessToken)
body "$RS" | grep -q '"mfaSetupRequired":true' && ok "owner: mfaSetupRequired=true (Mfa açık)" || bad "mfaSetupRequired yok — backend Mfa__Enabled=true mi?"

expect "mfa_pending → mobil logout (allowlist) → 200" 200 "$(code "$(post /api/auth/mobile/logout "{}" "$AT")")"
expect "mfa_pending → mobil refresh (allowlist DIŞI) → 403" 403 "$(code "$(post /api/auth/mobile/refresh "{\"refreshToken\":\"x\"}" "$AT")")"

SECRET=$(body "$(post /api/auth/mfa/setup/start "{}" "$AT")" | jget manualKey)
SV=$(posth /api/auth/mobile/mfa/setup/verify "{\"code\":\"$(totp "$SECRET")\"}" "$AT")
expect "F1: mfa_pending token → mobil mfa/setup/verify → 200" 200 "$(code "$SV")"
body "$SV" | grep -q '"refreshToken"' && ok "setup verify: refresh BODY'de" || bad "setup verify: refresh yok"
no_cookie "mobil setup verify"; no_store "mobil setup verify"
RC=$(body "$SV" | sed -n 's/.*"recoveryCodes":\["\([^"]*\)".*/\1/p')
[ -n "$RC" ] && ok "recovery code alındı" || bad "recovery code yok"

ML2=$(post /api/auth/mobile/login "{\"email\":\"$OEMAIL\",\"password\":\"$PW\"}")
body "$ML2" | grep -q '"mfaChallengeRequired":true' && ok "mobil login: MFA challenge" || bad "challenge bekleniyordu"
CT1=$(body "$ML2" | jget challengeToken)

CV=$(posth /api/auth/mobile/mfa/challenge/verify "{\"challengeToken\":\"$CT1\",\"code\":\"$(totp "$SECRET" 1)\",\"rememberDevice\":true}")
expect "mobil challenge verify → 200" 200 "$(code "$CV")"
TDT=$(body "$CV" | jget trustedDeviceToken)
[ -n "$TDT" ] && ok "rememberDevice: trustedDeviceToken BODY'de" || bad "trusted token body'de YOK"
TUNIX=$(body "$CV" | jgetn trustedDeviceExpiresAtUnix)
{ [ -n "$TUNIX" ] && [ $(( (TUNIX - $(date +%s)) / 86400 )) -ge 28 ] && [ $(( (TUNIX - $(date +%s)) / 86400 )) -le 31 ]; } \
  && ok "K6: trustedDeviceExpiresAtUnix ~30 gün (epoch)" || bad "K6: trustedDeviceExpiresAtUnix eksik/yanlış ($TUNIX)"
no_cookie "mobil challenge verify"; no_store "mobil challenge verify"

ML3=$(post /api/auth/mobile/login "{\"email\":\"$OEMAIL\",\"password\":\"$PW\",\"trustedDeviceToken\":\"$TDT\"}")
expect "trusted device ile login → 200" 200 "$(code "$ML3")"
body "$ML3" | grep -q '"mfaChallengeRequired":true' && bad "trusted: challenge geldi (atlanmalıydı)" || ok "trusted: challenge ATLANDI"
body "$ML3" | grep -q '"accessToken"' && ok "trusted: tam token döndü" || bad "trusted: tam token yok"

echo "== 8) Recovery-code: trusted device ÜRETMEZ =="
CT2=$(body "$(post /api/auth/mobile/login "{\"email\":\"$OEMAIL\",\"password\":\"$PW\"}")" | jget challengeToken)
RV=$(posth /api/auth/mobile/mfa/recovery-code/verify "{\"challengeToken\":\"$CT2\",\"recoveryCode\":\"$RC\"}")
expect "mobil recovery verify → 200" 200 "$(code "$RV")"
body "$RV" | grep -q '"trustedDeviceToken"' && bad "recovery: trustedDeviceToken DÖNDÜ (AGENTS.md §10.1 ihlali)" || ok "recovery: trusted device YOK"
body "$RV" | grep -q '"refreshToken"' && ok "recovery: refresh BODY'de" || bad "recovery: refresh yok"
no_cookie "mobil recovery verify"; no_store "mobil recovery verify"

echo "== 9) Audit hijyeni: Detail MASKELİ (ham token / uzun hash yok) =="
if [ "$AUDIT_ON" = "1" ]; then
  LEAK=$(audit_q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='RefreshTokenReuseDetected' AND \"Detail\" ~ '[A-Za-z0-9+/=]{20,}'")
  [ "$LEAK" = "0" ] && ok "audit Detail'de 20+ karakterlik token/hash dizisi YOK (maskeli)" || bad "audit Detail'de uzun token/hash dizisi VAR ($LEAK satır)"
  GT=$(reuse_count "grace=true"); GF=$(reuse_count "grace=false")
  { [ "$GT" -ge 1 ] && [ "$GF" -ge 1 ]; } && ok "audit özeti: grace=true=$GT, grace=false=$GF satır mevcut" || bad "audit özeti eksik (grace=true=$GT, grace=false=$GF)"
else skip "audit hijyen asserti (DB yok)"; fi

echo
[ "$FAILED" = "0" ] && { printf "\033[32mSPRINT 53 SMOKE GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
