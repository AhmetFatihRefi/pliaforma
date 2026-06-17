#!/usr/bin/env bash
# Pliaforma — release öncesi TAM doğrulama. Build + compose config + smoke + E2E.
# ÖN KOŞUL (smoke/E2E için): backend :5132 + frontend :3000 çalışıyor (native veya compose),
# postgres konteyneri ayakta (PlatformAdmin tohumlama + seed için psql gerekir).
# Kullanım: scripts/run-full-validation.sh [BASE]   (vars. http://localhost:5132)
#   Same-origin doğrulaması için BASE=http://localhost:3000 verilebilir (Next/nginx proxy).
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
ROOT="$DIR/../.."
API="$DIR/.."
FRONTEND="$ROOT/pliaformaFrontend"
STEP=0; FAIL=0
run(){ STEP=$((STEP+1)); printf "\n[%d] %s\n" "$STEP" "$1"; shift; if "$@"; then echo "  ✅ OK"; else echo "  ❌ FAIL"; FAIL=1; fi; }

run "dotnet build (0 hata)" bash -c "dotnet build '$API/pliaformaApi.sln' -nologo -clp:ErrorsOnly"
run "npm run build (frontend)" bash -c "cd '$FRONTEND' && npm run build >/tmp/pf-next-build.log 2>&1"
run "compose dev config" bash -c "docker compose -f '$ROOT/docker-compose.dev.yml' config >/dev/null"
run "compose prod config" bash -c "docker compose -f '$ROOT/docker-compose.prod.yml' config >/dev/null"
run "smoke + güvenlik (BASE=$BASE)" bash "$DIR/run-smokes.sh" "$BASE"
run "E2E (seed + Playwright)" bash "$DIR/run-e2e.sh" "$BASE"

echo
[ "$FAIL" = "0" ] && { echo "FULL VALIDATION: TÜM ADIMLAR GEÇTİ ✅"; exit 0; } || { echo "FULL VALIDATION: BAŞARISIZ ❌"; exit 1; }
