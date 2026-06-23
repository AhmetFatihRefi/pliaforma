#!/usr/bin/env bash
# Pliaforma — Sprint 31C-2 CSP (enforce + nonce + report-sink) smoke.
# STATİK: nginx CSP üretmez; proxy.ts policy doğru. RUNTIME (opsiyonel): Next CSP header + nonce + report endpoint.
# Kullanım:
#   scripts/csp-smoke.sh                                  # yalnız statik
#   CSP_FE_BASE=http://localhost:3100 CSP_API_BASE=http://localhost:5232 [CSP_EXPECT=enforce] scripts/csp-smoke.sh
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NGINX="$ROOT/deploy/nginx.conf"
PROXY="$ROOT/pliaformaFrontend/src/proxy.ts"
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }

echo "== STATİK: nginx CSP üretmiyor (Next sahiplenir) =="
if grep -qiE 'add_header[[:space:]]+Content-Security-Policy' "$NGINX"; then
  bad "nginx hâlâ CSP header ekliyor (çift CSP riski)"
else
  ok "nginx CSP header üretmiyor"
fi

echo "== STATİK: proxy.ts policy =="
[ -f "$PROXY" ] && ok "proxy.ts var" || bad "proxy.ts yok"
grep -q "report-uri /api/csp-report" "$PROXY" && ok "report-uri /api/csp-report var" || bad "report-uri yok"
grep -q "'strict-dynamic'" "$PROXY" && ok "script-src strict-dynamic var" || bad "strict-dynamic yok"
grep -q "nonce-" "$PROXY" && ok "script-src nonce var" || bad "nonce yok"
# script-src DİREKTİF satırında 'unsafe-inline' OLMAMALI (yorum satırları "script-src:" formundadır, eşleşmez):
if grep -E "script-src '" "$PROXY" | grep -q "unsafe-inline"; then bad "script-src'de unsafe-inline VAR (olmamalı)"; else ok "script-src'de unsafe-inline yok"; fi
grep -E "style-src '" "$PROXY" | grep -q "unsafe-inline" && ok "style-src unsafe-inline var" || bad "style-src unsafe-inline yok"

# ---- RUNTIME (opsiyonel) ----
if [ -n "${CSP_FE_BASE:-}" ]; then
  echo "== RUNTIME: Next CSP header ($CSP_FE_BASE) =="
  H=$(curl -sk -D - -o /dev/null "$CSP_FE_BASE/login" | tr -d '\r')
  ENF=$(echo "$H" | grep -ci '^content-security-policy:')
  RO=$(echo "$H" | grep -ci '^content-security-policy-report-only:')
  [ "$ENF" -le 1 ] && [ "$RO" -le 1 ] && [ $((ENF+RO)) -ge 1 ] && ok "tek CSP header (enf=$ENF ro=$RO)" || bad "CSP header sayısı hatalı (enf=$ENF ro=$RO)"
  [ "$ENF" -ge 1 ] && [ "$RO" -ge 1 ] && bad "AYNI ANDA enforce + report-only var" || ok "iki CSP header aynı anda yok"
  CSP=$(echo "$H" | grep -iE '^content-security-policy(-report-only)?:' | head -1)
  echo "$CSP" | grep -q "'nonce-" && ok "CSP nonce içeriyor" || bad "CSP nonce içermiyor"
  echo "$CSP" | grep -q "'strict-dynamic'" && ok "CSP strict-dynamic içeriyor" || bad "strict-dynamic yok"
  echo "$CSP" | grep -q "report-uri /api/csp-report" && ok "CSP report-uri içeriyor" || bad "report-uri yok"
  # script-src segmentinde unsafe-inline olmamalı (style-src'deki sayılmaz):
  SS=$(echo "$CSP" | grep -oE "script-src[^;]*")
  echo "$SS" | grep -q "unsafe-inline" && bad "runtime script-src'de unsafe-inline" || ok "runtime script-src'de unsafe-inline yok"
  if [ -n "${CSP_EXPECT:-}" ]; then
    if [ "$CSP_EXPECT" = "enforce" ]; then [ "$ENF" = "1" ] && [ "$RO" = "0" ] && ok "mode=enforce doğru" || bad "enforce beklendi (enf=$ENF ro=$RO)";
    else [ "$RO" = "1" ] && [ "$ENF" = "0" ] && ok "mode=report-only doğru" || bad "report-only beklendi (enf=$ENF ro=$RO)"; fi
  fi
  echo "== RUNTIME: HTML nonce =="
  B=$(curl -sk "$CSP_FE_BASE/login")
  NC=$(echo "$B" | grep -oE 'nonce="[^"]+"' | wc -l)
  [ "$NC" -ge 2 ] && ok "HTML'de nonce'lu script var (n=$NC: tema + framework)" || bad "HTML'de yeterli nonce yok (n=$NC)"
fi

if [ -n "${CSP_API_BASE:-}" ]; then
  echo "== RUNTIME: report endpoint ($CSP_API_BASE/api/csp-report) =="
  V=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$CSP_API_BASE/api/csp-report" -H 'Content-Type: application/csp-report' \
      -d '{"csp-report":{"document-uri":"https://x/login?token=SECRET","violated-directive":"script-src","blocked-uri":"https://evil.test/a.js?k=v"}}')
  [ "$V" = "204" ] && ok "geçerli rapor → 204" || bad "geçerli rapor → $V (204 bekleniyordu)"
  G=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$CSP_API_BASE/api/csp-report" -H 'Content-Type: application/json' -d 'not-json{{{')
  [ "$G" = "204" ] && ok "geçersiz gövde → 204 (crash yok)" || bad "geçersiz gövde → $G"
  BIG=$(head -c 9000 /dev/zero | tr '\0' 'a')
  O=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "$CSP_API_BASE/api/csp-report" -H 'Content-Type: application/json' -d "$BIG")
  [ "$O" = "413" ] && ok "oversize (9KB) → 413" || bad "oversize → $O (413 bekleniyordu)"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[32mCSP SMOKE GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
