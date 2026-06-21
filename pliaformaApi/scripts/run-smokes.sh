#!/usr/bin/env bash
# Pliaforma — tüm API smoke + güvenlik denetimi tek koşuda. ÖN KOŞUL: backend BASE'de çalışıyor.
# Kullanım: scripts/run-smokes.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
SMOKES=(auth-smoke sprint1-smoke role-permission-smoke sprint2-smoke sprint3-smoke sprint4-smoke
        sprint5-smoke sprint6-smoke sprint7-smoke sprint8-smoke sprint9-smoke sprint10-smoke
        sprint13-smoke sprint14-smoke sprint15-smoke sprint16-smoke sprint17-smoke sprint18a-smoke sprint18b-smoke sprint19-smoke sprint20-smoke sprint21-smoke sprint22-smoke sprint23-smoke sprint24-smoke sprint25-smoke sprint26-smoke sprint27-smoke sprint28a-smoke sprint28b-smoke sprint28c1-smoke sprint28c2-smoke timezone-smoke security-audit-smoke)
FAIL=0; PASSN=0
echo "== Smoke koşusu (BASE=$BASE) =="
for s in "${SMOKES[@]}"; do
  printf "  %-24s " "$s"
  if bash "$DIR/$s.sh" "$BASE" >/tmp/pf-$s.log 2>&1; then
    echo "PASS"; PASSN=$((PASSN+1))
  else
    echo "FAIL (bkz. /tmp/pf-$s.log)"; FAIL=1
  fi
done
echo "-- $PASSN/${#SMOKES[@]} geçti --"
[ "$FAIL" = "0" ] && { echo "TÜM SMOKE GEÇTİ ✅"; exit 0; } || { echo "SMOKE BAŞARISIZ ❌"; exit 1; }
