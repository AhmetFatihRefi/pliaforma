#!/usr/bin/env bash
# Pliaforma — E2E / demo seed (YALNIZCA DEV/TEST). Deterministic sabit kimlikler + demo veri.
# Önce e2e-* hesaplarının verisini psql ile TEMİZLER (idempotent/re-runnable), sonra sıfırdan kurar.
# Kullanım: scripts/e2e-seed.sh [BASE]   (vars. http://localhost:5132)
# Prod'a karışmaz: yalnızca e2e-* e-postalı kullanıcılar ve onların stüdyoları etkilenir.
set -u
BASE="${1:-http://localhost:5132}"
PW="E2ePass123!"
ok(){ printf "  \033[32m✓\033[0m %s\n" "$1"; }
warn(){ printf "  \033[33m!\033[0m %s\n" "$1"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }

PADMIN=e2e-padmin@pliaforma.local
OWNER=e2e-owner@pliaforma.local
ADMIN=e2e-admin@pliaforma.local
INSTR=e2e-instructor@pliaforma.local
INSTR2=e2e-instructor2@pliaforma.local
STUDENT=e2e-student@pliaforma.local
OWNER2=e2e-owner2@pliaforma.local

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (seed reset/backdate gerekli)."; exit 2; }
psql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" >/dev/null 2>&1; }

echo "== 0) Reset (yalnızca e2e-* verisi) =="
psql "DELETE FROM \"Studios\" WHERE \"OwnerUserId\" IN (SELECT \"Id\" FROM \"Users\" WHERE \"Email\" LIKE 'e2e-%');"
psql "DELETE FROM \"Users\" WHERE \"Email\" LIKE 'e2e-%';"
ok "Önceki e2e verisi temizlendi"

echo "== 1) PlatformAdmin =="
post /api/auth/register "{\"fullName\":\"E2E Platform Admin\",\"email\":\"$PADMIN\",\"password\":\"$PW\"}" >/dev/null
psql "UPDATE \"Users\" SET \"GlobalRole\"='PlatformAdmin' WHERE \"Email\"='$PADMIN';"
ok "PlatformAdmin: $PADMIN / $PW"

echo "== 2) Owner + ana stüdyo =="
OR=$(post /api/auth/register-studio "{\"studioName\":\"E2E Pilates Studio\",\"ownerFullName\":\"E2E Owner\",\"email\":\"$OWNER\",\"password\":\"$PW\"}")
OTOK=$(body "$OR" | jget accessToken)
ok "Owner: $OWNER (stüdyo: E2E Pilates Studio)"

echo "== 3) StudioAdmin + 2 Instructor (davet+kabul) =="
acceptMember(){ # email role [rate] -> profileId
  local email="$1" role="$2" rate="${3:-}" body resp m tok
  post /api/auth/register "{\"fullName\":\"$email\",\"email\":\"$email\",\"password\":\"$PW\"}" >/dev/null
  resp=$(post /api/studio/memberships/invite "{\"email\":\"$email\",\"role\":\"$role\"${rate:+,\"rate\":$rate}}" "$OTOK")
  m=$(body "$resp" | jget membershipId); tok=$(login "$email")
  post "/api/members/$m/accept" '{}' "$tok" >/dev/null
  body "$resp" | jget instructorProfileId
}
acceptMember "$ADMIN" StudioAdmin >/dev/null; ok "StudioAdmin: $ADMIN"
I1P=$(acceptMember "$INSTR" Instructor 200); ok "Instructor: $INSTR (profil $I1P)"
# 2. hoca: müsaitlik penceresi YOK → Owner bu hoca için herhangi bir saatte seans açabilir
# (zamanı sabit olmayan demo seansları bunun üzerinden oluşturulur; deterministic).
I2P=$(acceptMember "$INSTR2" Instructor 180); ok "Instructor 2: $INSTR2 (profil $I2P)"

echo "== 4) ClassType + Availability =="
CT=$(body "$(post /api/class-types '{"name":"E2E Reformer","equipment":"Reformer","capacityType":"Group","maxCapacity":6,"level":"AllLevels","purpose":"General","defaultDurationMinutes":60}' "$OTOK")" | jget id)
post "/api/studio/instructors/$I1P/availabilities" '{"dayOfWeek":"Monday","startTime":"09:00:00","endTime":"18:00:00"}' "$OTOK" >/dev/null
ok "ClassType E2E Reformer + müsaitlik"

echo "== 5) Öğrenciler (1 user + 1 guest) =="
mksess(){ local r id; r=$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CT\",\"startAt\":\"$1\",\"capacity\":6}" "$OTOK"); id=$(body "$r" | jget id); [ -z "$id" ] && echo "MKSESS FAIL [$1]: $(body "$r")" >&2; echo "$id"; }
GS=$(body "$(post /api/studio/students/guest '{"fullName":"E2E Student","source":"Phone"}' "$OTOK")" | jget id)
post "/api/studio/students/$GS/convert-to-user" "{\"email\":\"$STUDENT\",\"initialPassword\":\"$PW\"}" "$OTOK" >/dev/null
ok "Student: $STUDENT (profil $GS)"
GG=$(body "$(post /api/studio/students/guest '{"fullName":"E2E Guest Misafir","source":"Phone"}' "$OTOK")" | jget id)
ok "Guest student (profil $GG)"

echo "== 6) Seanslar + rezervasyon + yoklama =="
IST(){ TZ='Europe/Istanbul' date "$@" 2>/dev/null || date "$@"; }
# Yaklaşan ders (~20h sonra → ClassReminder penceresi içinde) + öğrenci rezervasyonu
SOON=$(mksess "$(IST -d '+20 hours' +%Y-%m-%dT%H:%M:00)")
body "$(post "/api/studio/class-sessions/$SOON/bookings" "{\"studentProfileId\":\"$GS\"}" "$OTOK")" >/dev/null
ok "Yaklaşan seans + öğrenci rezervasyonu"
# Geçmiş ders (backdate) + yoklama (Present)
PASTS=$(mksess "$(IST -d '+2 days' +%Y-%m-%dT10:00:00)")
PBOOK=$(body "$(post "/api/studio/class-sessions/$PASTS/bookings" "{\"studentProfileId\":\"$GS\"}" "$OTOK")" | jget id)
psql "UPDATE \"ClassSessions\" SET \"StartAt\"='2020-01-06 10:00:00', \"EndAt\"='2020-01-06 11:00:00' WHERE \"Id\"='$PASTS';"
post "/api/studio/bookings/$PBOOK/attendance" '{"status":"Present"}' "$OTOK" >/dev/null
ok "Geçmiş seans + yoklama (Present)"

echo "== 7) Paket + ödemeler + telafi kredisi =="
PLAN=$(body "$(post /api/studio/package-plans '{"name":"E2E 8 Ders Paketi","creditCount":8,"price":2400,"validityDays":90}' "$OTOK")" | jget id)
post "/api/studio/students/$GS/packages" "{\"packagePlanId\":\"$PLAN\"}" "$OTOK" >/dev/null
DUESOON=$(IST -d '+1 day' +%Y-%m-%d)
post "/api/studio/students/$GS/payments" "{\"amount\":600,\"dueDate\":\"$DUESOON\"}" "$OTOK" >/dev/null            # due-soon
PJ=$(post "/api/studio/students/$GS/payments" "{\"amount\":600,\"dueDate\":\"2020-02-01\"}" "$OTOK"); PP=$(body "$PJ" | jget id)
post "/api/studio/payments/$PP/mark-paid" '{}' "$OTOK" >/dev/null                                                  # paid
post "/api/studio/students/$GS/payments" "{\"amount\":600,\"dueDate\":\"2020-01-01\"}" "$OTOK" >/dev/null          # overdue
post "/api/studio/students/$GS/makeup-credits/grant" '{"amount":1,"notes":"E2E demo"}' "$OTOK" >/dev/null
ok "Paket + due-soon/paid/overdue ödeme + telafi kredisi"

echo "== 8) Bildirim üretimi (reminder trigger) =="
post /api/studio/notifications/run-reminders '{}' "$OTOK" >/dev/null
ok "Bildirimler üretildi (öğrenciye ders/ödeme hatırlatması)"

echo "== 9) İkinci stüdyo (askıya alınabilir test stüdyosu) =="
post /api/auth/register-studio "{\"studioName\":\"E2E Test Studio\",\"ownerFullName\":\"E2E Owner2\",\"email\":\"$OWNER2\",\"password\":\"$PW\"}" >/dev/null
ok "Test stüdyosu: E2E Test Studio (owner $OWNER2)"

echo
printf "\033[36mE2E SEED TAMAM ✅\033[0m  (tüm parolalar: %s)\n" "$PW"
echo "PlatformAdmin=$PADMIN  Owner=$OWNER  StudioAdmin=$ADMIN  Instructor=$INSTR  Student=$STUDENT"
