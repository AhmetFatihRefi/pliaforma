#!/usr/bin/env bash
# Pliaforma — Sprint 31B-2 CSP (Report-Only) smoke.
# nginx config'inde Content-Security-Policy-Report-Only var ve ENFORCE (bloklayan) CSP YOK.
# Opsiyonel runtime: NGINX_BASE verilirse yanıt header'ı doğrulanır.
# Kullanım: scripts/csp-smoke.sh   |   NGINX_BASE=https://localhost scripts/csp-smoke.sh
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NGINX="$ROOT/deploy/nginx.conf"
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }

echo "== STATİK: nginx CSP =="
grep -q 'Content-Security-Policy-Report-Only' "$NGINX" && ok "Report-Only header tanımlı" || bad "Report-Only header yok"
# Enforce eden (Report-Only OLMAYAN) bir CSP header eklenmemiş olmalı:
if grep -E 'add_header[[:space:]]+Content-Security-Policy[[:space:]]' "$NGINX" | grep -qv 'Report-Only'; then
  bad "ENFORCE eden CSP header var (bu sprintte yalnız Report-Only olmalı)"
else
  ok "enforce eden CSP yok (yalnız Report-Only)"
fi
grep -q "default-src 'self'" "$NGINX" && ok "policy default-src 'self' içeriyor" || bad "policy beklenen direktifleri içermiyor"

if [ -n "${NGINX_BASE:-}" ]; then
  echo "== RUNTIME: $NGINX_BASE =="
  H=$(curl -skI "$NGINX_BASE/" | tr -d '\r')
  echo "$H" | grep -qi '^Content-Security-Policy-Report-Only:' && ok "yanıt Report-Only header içeriyor" || bad "yanıtta Report-Only header yok"
  echo "$H" | grep -i '^Content-Security-Policy:' | grep -qv 'Report-Only' && bad "yanıtta ENFORCE CSP var" || ok "yanıtta enforce CSP yok"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[32mCSP (REPORT-ONLY) GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ\033[0m\n"; exit 1; }
