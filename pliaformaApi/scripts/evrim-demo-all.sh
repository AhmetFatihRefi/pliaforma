#!/usr/bin/env bash
# Evrim demo — tam kurulum (sıfırdan): stüdyo+hocalar+öğrenciler+sözleşmeler+ödemeler (seed) +
# paylaşımlı grup dersleri & geçmiş işlenmiş (extra) + tüm öğrencileri user yap + verified + aktif.
set -u
BASE="${1:-http://localhost:5132}"
DIR="$(dirname "$0")"
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)

bash "$DIR/evrim-demo-seed.sh" "$BASE" || exit 1
bash "$DIR/evrim-demo-extra.sh" || exit 1
echo "== Tüm öğrencileri user yap =="
node "$DIR/evrim-convert-users.mjs" "$BASE" "Demo!2345"

SID=$(docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='Evrim Mutlu Pilates Studyo' LIMIT 1;" 2>/dev/null)
docker exec "$PGC" psql -U postgres -d pliaforma -c "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\" LIKE '%@demo.local' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null 2>&1
docker exec "$PGC" psql -U postgres -d pliaforma -c "UPDATE \"StudentProfiles\" SET \"Status\"='Active' WHERE \"StudioId\"='$SID' AND \"Status\"='Guest';" >/dev/null 2>&1
echo
echo "EVRIM DEMO (tam) HAZIR ✅"
