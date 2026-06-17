#!/usr/bin/env bash
# Pliaforma — E2E koşusu: deterministic seed + Playwright. ÖN KOŞUL: backend (:5132/proxy) + frontend (:3000).
# Kullanım: scripts/run-e2e.sh [SEED_BASE]   (vars. http://localhost:5132)
set -u
SEED_BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
FRONTEND="$DIR/../../pliaformaFrontend"

echo "== 1) E2E seed (DEV/TEST) =="
bash "$DIR/e2e-seed.sh" "$SEED_BASE" >/tmp/pf-e2e-seed.log 2>&1 || { echo "SEED BAŞARISIZ ❌ (bkz. /tmp/pf-e2e-seed.log)"; exit 1; }
echo "  seed tamam"

echo "== 2) Playwright (headless) =="
( cd "$FRONTEND" && npm run test:e2e )
