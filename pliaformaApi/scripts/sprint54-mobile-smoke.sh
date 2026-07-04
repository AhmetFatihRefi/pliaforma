#!/usr/bin/env bash
# Pliaforma — Sprint 54 mobil temel STATİK smoke (backend gerektirmez).
# Mobil AGENTS.md + kök AGENTS.md §10.2 kurallarının kod düzeyinde ihlal edilmediğini denetler:
# secure-store tek modülde, AsyncStorage'da token yok, yalnız mobile auth uçları, cookie yok,
# token loglama yok, EXPO_PUBLIC secret yok, dashboard salt-okunur, expiry Unix/JWT-exp tabanlı.
# Kullanım: scripts/sprint54-mobile-smoke.sh [MOBILE_DIR]
set -u
DIR="${1:-$(dirname "$0")/../../pliaformaMobile}"
SRC="$DIR/src"
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
# kaynakta desen VAR mı (0=var)
has(){ grep -rq --include='*.ts' --include='*.tsx' -E "$1" "$SRC" 2>/dev/null; }
# F3: Tüm kaynak, yalnız SATIR BAŞI (veya baştaki boşluk sonrası) // yorumları düşülerek akıtılır.
# String içindeki https:// BOZULMAZ → güvenlik denetimleri URL'li satırlarda körleşmez.
code_stream(){ grep -rh --include='*.ts' --include='*.tsx' "" "$SRC" 2>/dev/null | sed -E 's|^[[:space:]]*//.*$||'; }
code_has(){ code_stream | grep -qE "$1"; }
# F3: string-concat/template kaçışlarına karşı normalize akış — bitişik string literallerinin
# birleştirilmesi ('a' + 'b' → ab) ve tırnak/backtick + ${} sınırlarının düşürülmesi.
code_norm(){ code_stream | sed -E "s/['\"\`][[:space:]]*\+[[:space:]]*['\"\`]//g; s/['\"\`]//g; s/\\\$\{|\}//g"; }

echo "== 1) İskelet ve zorunlu dosyalar =="
[ -d "$DIR" ] && ok "pliaformaMobile/ var" || { bad "pliaformaMobile/ YOK"; exit 1; }
[ -f "$DIR/AGENTS.md" ] && ok "pliaformaMobile/AGENTS.md var" || bad "mobil AGENTS.md YOK"
grep -q "10.2" "$DIR/AGENTS.md" && ok "mobil AGENTS.md kök §10.2'ye referans veriyor" || bad "§10.2 referansı yok"
[ -f "$DIR/.env.example" ] && ok ".env.example var" || bad ".env.example yok"
grep -q '"typecheck"' "$DIR/package.json" && ok "package.json script'leri (typecheck) var" || bad "typecheck script'i yok"
grep -q '"expo-secure-store"' "$DIR/package.json" && ok "expo-secure-store dependency var" || bad "expo-secure-store YOK"
# F4: tool-specific/local dosyalar commit'e giremez; .env.example TEK istisna.
git -C "$DIR" check-ignore -q CLAUDE.md && ok "CLAUDE.md ignore ediliyor" || bad "CLAUDE.md ignore EDİLMİYOR"
git -C "$DIR" check-ignore -q .claude/settings.json && ok ".claude/ ignore ediliyor" || bad ".claude/ ignore EDİLMİYOR"
git -C "$DIR" check-ignore -q .env && ok ".env ignore ediliyor" || bad ".env ignore EDİLMİYOR"
git -C "$DIR" check-ignore -q .env.example && bad ".env.example YANLIŞLIKLA ignore ediliyor" || ok ".env.example commit'lenebilir"
# F5: budanan şablon bağımlılıkları geri gelmesin (K2: UI library yok, minimal deps).
if grep -qE '"@expo/ui"|"expo-glass-effect"|"react-native-web"|"react-dom"' "$DIR/package.json"; then
  bad "budanmış şablon bağımlılığı package.json'a geri gelmiş"
else
  ok "kullanılmayan şablon bağımlılıkları yok (@expo/ui, glass-effect, rn-web, react-dom)"
fi

echo "== 2) TypeScript =="
( cd "$DIR" && npx tsc --noEmit >/dev/null 2>&1 ) && ok "tsc --noEmit geçiyor" || bad "tsc HATALI"

echo "== 3) Token depolama kuralları =="
code_has "AsyncStorage" && bad "AsyncStorage kullanımı VAR (token depolama riski — yasak)" || ok "AsyncStorage kullanımı yok (kodda)"
SS_FILES=$(grep -rl --include='*.ts' --include='*.tsx' "expo-secure-store" "$SRC" 2>/dev/null)
[ "$SS_FILES" = "$SRC/auth/tokenStore.ts" ] && ok "SecureStore YALNIZ tokenStore.ts'te" || bad "SecureStore başka modülde de var: $SS_FILES"
has "setItemAsync\(.?REFRESH" && true # bilgi amaçlı; anahtar adları tokenStore'da sabit
grep -q "pf_refresh_token" "$SRC/auth/tokenStore.ts" && ok "refresh token SecureStore anahtarı tanımlı" || bad "refresh anahtarı yok"

echo "== 4) Auth taşıma kuralları =="
has "/api/auth/mobile/login" && has "/api/auth/mobile/refresh" && has "/api/auth/mobile/logout" \
  && ok "mobil auth uçları kullanılıyor (login/refresh/logout)" || bad "mobil auth uçları eksik"
has "/api/auth/mobile/mfa/challenge/verify" && has "/api/auth/mobile/mfa/recovery-code/verify" \
  && ok "mobil MFA uçları kullanılıyor" || bad "mobil MFA uçları eksik"
# F3: web-refresh yasağı TIRNAK-BAĞIMSIZ — önce mobil yol maskelenir, kalan her "api/auth/refresh"
# (concat/template dahil) ihlaldir. Aynı yaklaşım web login taşıması için de uygulanır.
if code_norm | sed 's|api/auth/mobile/refresh||g' | grep -q "api/auth/refresh"; then
  bad "WEB refresh ucu mobil kodda VAR (yasak)"
else
  ok "web refresh ucu mobil kodda YOK (tırnak/concat/template-bağımsız)"
fi
if code_norm | sed 's|api/auth/mobile/login||g' | grep -q "api/auth/login"; then
  bad "WEB login taşıma ucu mobil kodda VAR (yasak)"
else
  ok "web login taşıma ucu mobil kodda YOK"
fi
# F3: credentials varyantları (: veya =, tırnaklı/tırnaksız) — 'omit' değeri serbesttir.
code_has "credentials[[:space:]]*[:=][[:space:]]*[\"']?include" && bad "credentials include VAR (yasak)" || ok "credentials include yok (kodda)"
grep -q "credentials: 'omit'" "$SRC/api/http.ts" && ok "rawRequest credentials:'omit' kullanıyor (F6.b)" || bad "credentials:'omit' YOK"
code_has "[Ss]et-[Cc]ookie|document\.cookie|Cookie(Jar|Manager)" && bad "cookie mantığı VAR (yasak)" || ok "cookie mantığı yok"

echo "== 5) Loglama hijyeni (F3: sıfır-console kapısı) =="
# Çok-satırlı argümanlar tek-satır desenlerden kaçabildiği için kapı KESİNDİR: kodda (yorum dışı)
# HERHANGİ bir console.* kullanımı FAIL'dir. Meşru bir log gerekirse bu kapı bilinçli olarak,
# reviewer onayıyla gevşetilir.
if code_has "console\."; then
  bad "console.* kullanımı VAR — token sızdırma riski (sıfır-console kuralı)"
else
  ok "console.* kullanımı yok (sıfır-console)"
fi

echo "== 6) EXPO_PUBLIC / base URL =="
if grep -rqE "EXPO_PUBLIC_[A-Z_]*(SECRET|KEY|PASSWORD|TOKEN)" "$DIR/.env.example" "$SRC" 2>/dev/null; then
  bad "EXPO_PUBLIC içinde secret-adlı değişken VAR"
else
  ok "EXPO_PUBLIC içinde secret yok"
fi
grep -q "https://pliaforma.com" "$SRC/api/http.ts" && ok "API base URL public default (pliaforma.com)" || bad "base URL default'u beklenmedik"

echo "== 7) Dashboard salt-okunur =="
APP_DIR="$SRC/app/(app)"
if grep -rqE "method:\s*'(POST|PUT|PATCH|DELETE)'" "$APP_DIR" 2>/dev/null; then
  bad "(app) grubunda WRITE isteği VAR (dashboard salt-okunur olmalı)"
else
  ok "(app) grubunda write isteği yok (account'taki logout AuthContext üzerindendir)"
fi

echo "== 8) Expiry: Unix/JWT-exp (DateTime parse YASAK) =="
has "ExpiresAtUnix" && ok "epoch (Unix) expiry alanları kullanılıyor" || bad "Unix expiry alanı kullanılmıyor"
# F3: satır-düzeyi 'grep -v Unix' muafiyeti KALDIRILDI — [^U] karakter sınıfı Unix-alanlarını zaten
# ayırt eder; aynı satırda "Unix" kelimesi geçmesi ihlali aklamaz.
if code_stream | grep -qE "(Date\.parse|new Date)\([^)]*[Ee]xpiresAt([^U]|\))"; then
  bad "DateTime expiry string'i parse ediliyor (yasak)"
else
  ok "expiry için DateTime string parse edilmiyor"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[32mSPRINT 54 MOBILE SMOKE GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
