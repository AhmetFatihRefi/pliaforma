#!/usr/bin/env bash
# Pliaforma — Sprint 31B-1 metrics/monitoring exposure smoke.
# /metrics + Grafana/Prometheus prod sertleştirmesini (P0-3) ve TLS/HSTS config'ini (P0-1) STATİK olarak doğrular.
# Çalışan stack GEREKTİRMEZ (config taraması). Opsiyonel runtime: NGINX_BASE verilirse nginx üzerinden curl eder.
#
# Kullanım:
#   scripts/metrics-exposure-smoke.sh                 # yalnız statik config kontrolleri
#   NGINX_BASE=https://localhost scripts/metrics-exposure-smoke.sh   # + runtime (self-signed için -k)
set -u

# Repo kökü (bu script pliaformaApi/scripts altında).
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NGINX="$ROOT/deploy/nginx.conf"
MON="$ROOT/docker-compose.monitoring.yml"
HEALTH="$ROOT/pliaformaFrontend/src/app/(platform)/platform/health/page.tsx"

FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
has(){ grep -qE "$2" "$1"; }            # has <file> <regex>
hasnt(){ ! grep -qE "$2" "$1"; }        # hasnt <file> <regex>

echo "== STATİK: nginx (/metrics + TLS/HSTS) =="
has "$NGINX" 'location = /metrics'                 && ok "nginx: defansif /metrics location var"        || bad "nginx: /metrics location yok"
grep -A2 'location = /metrics' "$NGINX" | grep -qE 'return (404|403)' && ok "nginx: /metrics 404/403 döner" || bad "nginx: /metrics 404/403 dönmüyor"
has "$NGINX" 'listen 443 ssl'                      && ok "nginx: 443 ssl bloğu aktif"                    || bad "nginx: 443 ssl yok"
has "$NGINX" 'return 301 https://'                 && ok "nginx: HTTP→HTTPS 301 redirect"               || bad "nginx: 301 redirect yok"
has "$NGINX" 'Strict-Transport-Security'           && ok "nginx: HSTS header var"                       || bad "nginx: HSTS yok"

echo "== STATİK: monitoring compose (Grafana/port) =="
[ "$(grep -cE '127\.0\.0\.1:(9090|3001):' "$MON")" -ge 2 ] && ok "compose: portlar 127.0.0.1 (loopback) bind" || bad "compose: portlar loopback bind değil"
hasnt "$MON" 'GF_SECURITY_ADMIN_PASSWORD:[[:space:]]*admin([[:space:]]|$)' && ok "compose: default admin/admin parola yok" || bad "compose: default admin parola var"
has "$MON" 'GF_ADMIN_PASSWORD:\?'                  && ok "compose: GF_ADMIN_PASSWORD env zorunlu"        || bad "compose: GF_ADMIN_PASSWORD zorunlu değil"
has "$MON" 'GF_AUTH_ANONYMOUS_ENABLED:.*GF_ANON_ENABLED:-false' && ok "compose: anonymous varsayılan kapalı" || bad "compose: anonymous varsayılan açık olabilir"

echo "== STATİK: platform health page =="
hasnt "$HEALTH" 'admin/admin'                      && ok "health page: 'admin/admin' metni yok"         || bad "health page: 'admin/admin' metni var"
hasnt "$HEALTH" 'http://localhost:(3001|9090|5132)' && ok "health page: hardcoded localhost link yok"   || bad "health page: hardcoded localhost link var"
has "$HEALTH" 'NEXT_PUBLIC_GRAFANA_URL'            && ok "health page: monitoring env-bazlı"            || bad "health page: env-bazlı değil"

echo "== STATİK: compose required-var davranışı =="
if command -v docker >/dev/null 2>&1; then
  if docker compose -f "$MON" config >/dev/null 2>&1; then
    bad "compose: GF_ADMIN_PASSWORD olmadan config GEÇTİ (zorunlu değil)"
  else
    ok "compose: GF_ADMIN_PASSWORD yokken config hata verir (zorunlu)"
  fi
  GF_ADMIN_PASSWORD=dummy docker compose -f "$MON" config >/dev/null 2>&1 \
    && ok "compose: GF_ADMIN_PASSWORD ile config VALID" || bad "compose: env'li config geçersiz"
else
  echo "  (docker yok — compose runtime kontrolü atlandı)"
fi

# Opsiyonel runtime (nginx ayakta + sertifika varsa).
if [ -n "${NGINX_BASE:-}" ]; then
  echo "== RUNTIME: $NGINX_BASE =="
  M=$(curl -sk -o /dev/null -w "%{http_code}" "$NGINX_BASE/metrics")
  { [ "$M" = "404" ] || [ "$M" = "403" ]; } && ok "/metrics dışarı kapalı ($M)" || bad "/metrics erişilebilir ($M)"
  H=$(curl -sk -o /dev/null -w "%{http_code}" "$NGINX_BASE/health")
  [ "$H" = "200" ] && ok "/health 200" || bad "/health $H"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[32mTÜM KONTROLLER GEÇTİ\033[0m\n"; exit 0; } || { printf "\033[31mBAŞARISIZ KONTROL VAR\033[0m\n"; exit 1; }
