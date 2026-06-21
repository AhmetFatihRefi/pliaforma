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
  # Sprint 17: davet kabulü (InvitedByStudio) doğrulanmış e-posta ister → kabulden önce verify.
  psql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$email' AND \"EmailVerifiedAt\" IS NULL;"
  resp=$(post /api/studio/memberships/invite "{\"email\":\"$email\",\"role\":\"$role\"${rate:+,\"rate\":$rate}}" "$OTOK")
  m=$(body "$resp" | jget membershipId); tok=$(login "$email")
  post "/api/members/$m/accept" '{}' "$tok" >/dev/null
  # Sprint 17: profil invite anında DEĞİL, kabulde oluşur → profileId üye listesinden (accept sonrası) alınır.
  get /api/studio/memberships "$OTOK" | tr '{' '\n' | grep "\"email\":\"$email\"" \
    | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'
}
acceptMember "$ADMIN" StudioAdmin >/dev/null; ok "StudioAdmin: $ADMIN"
I1P=$(acceptMember "$INSTR" Instructor 200); ok "Instructor: $INSTR (profil $I1P)"
# 2. hoca: müsaitlik penceresi YOK → Owner bu hoca için herhangi bir saatte seans açabilir
# (zamanı sabit olmayan demo seansları bunun üzerinden oluşturulur; deterministic).
I2P=$(acceptMember "$INSTR2" Instructor 180); ok "Instructor 2: $INSTR2 (profil $I2P)"

# Sprint 17 Rev2: stüdyosuz davetli — INSTRUCTOR intent + bekleyen hoca daveti (Davetlerim/hoca empty-state E2E).
INVITEE=e2e-invitee@pliaforma.local
post /api/auth/register "{\"fullName\":\"E2E Invitee\",\"email\":\"$INVITEE\",\"password\":\"$PW\",\"intent\":\"Instructor\"}" >/dev/null
psql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$INVITEE';"
post /api/studio/memberships/invite "{\"email\":\"$INVITEE\",\"role\":\"Instructor\"}" "$OTOK" >/dev/null
ok "Stüdyosuz davetli (Instructor intent + bekleyen davet): $INVITEE"

# Sprint 17 Rev2: STUDENT intent stüdyosuz kullanıcı (JoinCode formu görünmeli — daveti/üyeliği yok).
NOJOIN=e2e-nojoin-student@pliaforma.local
post /api/auth/register "{\"fullName\":\"E2E NoJoin Student\",\"email\":\"$NOJOIN\",\"password\":\"$PW\",\"intent\":\"Student\"}" >/dev/null
psql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$NOJOIN';"
ok "Stüdyosuz öğrenci (Student intent): $NOJOIN"

echo "== 4) ClassType =="
# Sprint 26: hoca müsaitlik (availability) kaldırıldı → seed availability oluşturmaz; program gerçek derslerden.
CT=$(body "$(post /api/class-types '{"name":"E2E Reformer","equipment":"Reformer","capacityType":"Group","maxCapacity":6,"level":"AllLevels","purpose":"General","defaultDurationMinutes":60}' "$OTOK")" | jget id)
ok "ClassType E2E Reformer"

echo "== 5) Öğrenciler (1 user + 1 guest) =="
mksess(){ local r id; r=$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I2P\",\"classTypeId\":\"$CT\",\"startAt\":\"$1\",\"capacity\":6}" "$OTOK"); id=$(body "$r" | jget id); [ -z "$id" ] && echo "MKSESS FAIL [$1]: $(body "$r")" >&2; echo "$id"; }
GS=$(body "$(post /api/studio/students/guest '{"fullName":"E2E Student","source":"Phone"}' "$OTOK")" | jget id)
post "/api/studio/students/$GS/convert-to-user" "{\"email\":\"$STUDENT\",\"initialPassword\":\"$PW\"}" "$OTOK" >/dev/null
ok "Student: $STUDENT (profil $GS)"
GG=$(body "$(post /api/studio/students/guest '{"fullName":"E2E Guest Misafir","source":"Phone"}' "$OTOK")" | jget id)
ok "Guest student (profil $GG)"

# Sprint 18a/18b: sözleşme-tabanlı program.
#  - Limitsiz grup: GS (E2E Student) + I1P, 10:00 tüm hafta → öğrenci programı + ClassReminder (günlük ders → her
#    zaman <25h içinde biri) + schedule/sözleşme E2E. total (30) > üretilen ders sayısı (today..+14 ≈ 15) →
#    HEADROOM kalır; section 6 geçmiş-ders demo booking'i bu sözleşmeden auto-resolve eder.
#  - Bireysel: GG (guest) + I1P, 14:00 tüm hafta → bireysel rozeti (15-contracts).  I1P 10:00≠14:00 → çakışma yok.
C18SD=$(date +%F); C18ED=$(date -d "+14 days" +%F)
ALLDAYS10='[{"dayOfWeek":"Monday","startTime":"10:00:00"},{"dayOfWeek":"Tuesday","startTime":"10:00:00"},{"dayOfWeek":"Wednesday","startTime":"10:00:00"},{"dayOfWeek":"Thursday","startTime":"10:00:00"},{"dayOfWeek":"Friday","startTime":"10:00:00"},{"dayOfWeek":"Saturday","startTime":"10:00:00"},{"dayOfWeek":"Sunday","startTime":"10:00:00"}]'
ALLDAYS14='[{"dayOfWeek":"Monday","startTime":"14:00:00"},{"dayOfWeek":"Tuesday","startTime":"14:00:00"},{"dayOfWeek":"Wednesday","startTime":"14:00:00"},{"dayOfWeek":"Thursday","startTime":"14:00:00"},{"dayOfWeek":"Friday","startTime":"14:00:00"},{"dayOfWeek":"Saturday","startTime":"14:00:00"},{"dayOfWeek":"Sunday","startTime":"14:00:00"}]'
GC=$(body "$(post "/api/studio/students/$GS/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$C18SD\",\"endDate\":\"$C18ED\",\"totalLessonCount\":30,\"makeupAllowance\":2,\"price\":4000,\"isIndividualDefault\":false}" "$OTOK")" | jget id)
post "/api/studio/contracts/$GC/schedule/generate" "{\"slots\":$ALLDAYS10,\"reuseExistingGroup\":false,\"individual\":null}" "$OTOK" >/dev/null
IC=$(body "$(post "/api/studio/students/$GG/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I1P\",\"startDate\":\"$C18SD\",\"endDate\":\"$C18ED\",\"totalLessonCount\":3,\"makeupAllowance\":0,\"price\":3000,\"isIndividualDefault\":true}" "$OTOK")" | jget id)
post "/api/studio/contracts/$IC/schedule/generate" "{\"slots\":$ALLDAYS14,\"reuseExistingGroup\":false,\"individual\":null}" "$OTOK" >/dev/null
ok "Sözleşme + program: GS limitsiz grup (10:00, headroom'lu) + GG bireysel (14:00)"

echo "== 6) Geçmiş ders + yoklama (yaklaşan dersler program üretiminden gelir) =="
IST(){ TZ='Europe/Istanbul' date "$@" 2>/dev/null || date "$@"; }
# Yaklaşan rezervasyon + ClassReminder: GS'in üretilen günlük 10:00 dersleri (sonraki ders her zaman <25h içinde) sağlar.
# Geçmiş ders (backdate) + yoklama (Present): GS'i I2P 16:00 seansına book.
# Sprint 28C-1: booking resolver instructor-zorunlu → GS'in I2P seansına booklanabilmesi için I2P line'ı gerekir.
post "/api/studio/students/$GS/contracts" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"$I2P\",\"startDate\":\"$C18SD\",\"endDate\":\"$C18ED\",\"totalLessonCount\":5,\"makeupAllowance\":1,\"price\":0,\"isIndividualDefault\":false}" "$OTOK" >/dev/null
PASTS=$(mksess "$(IST -d '+2 days' +%Y-%m-%dT16:00:00)")
PBOOK=$(body "$(post "/api/studio/class-sessions/$PASTS/bookings" "{\"studentProfileId\":\"$GS\"}" "$OTOK")" | jget id)
psql "UPDATE \"ClassSessions\" SET \"StartAt\"='2020-01-06 10:00:00', \"EndAt\"='2020-01-06 11:00:00' WHERE \"Id\"='$PASTS';"
# Tekil yoklama ucu PUT'tur (POST → 405). Sessiz başarısızlığa karşı: HTTP kodu kontrol + Attendance kaydı doğrula.
ATT_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE/api/studio/bookings/$PBOOK/attendance" -H 'Content-Type: application/json' -H "Authorization: Bearer $OTOK" -d '{"status":"Present"}')
case "$ATT_CODE" in 2*) ;; *) echo "  HATA: geçmiş ders yoklaması başarısız (HTTP $ATT_CODE)"; exit 1;; esac
ATT_OK=$(docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "SELECT count(*) FROM \"Attendances\" WHERE \"ClassBookingId\"='$PBOOK';" 2>/dev/null)
[ "${ATT_OK:-0}" -ge 1 ] || { echo "  HATA: yoklama HTTP $ATT_CODE döndü ama Attendance kaydı oluşmadı"; exit 1; }
ok "Geçmiş seans + yoklama (Present, HTTP $ATT_CODE, kayıt doğrulandı); yaklaşan rezervasyonlar programdan"

echo "== 7) Ödemeler + telafi kredisi (Sprint 18b: paket satışı kaldırıldı — paket modeli dormant) =="
DUESOON=$(IST -d '+1 day' +%Y-%m-%d)
# Sprint 28C-2/29A: manuel grant + ödeme line zorunlu → GS'in grup line'ına (GC) bağla (dueDate NULL → LinePaymentDue tetiklemez).
GLINE=$(docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "SELECT \"Id\" FROM \"StudentContractLines\" WHERE \"StudentContractId\"='$GC' ORDER BY \"SequenceNo\" LIMIT 1;" 2>/dev/null)
post "/api/studio/students/$GS/payments" "{\"amount\":600,\"dueDate\":\"$DUESOON\",\"studentContractLineId\":\"$GLINE\"}" "$OTOK" >/dev/null            # due-soon
PJ=$(post "/api/studio/students/$GS/payments" "{\"amount\":600,\"dueDate\":\"2020-02-01\",\"studentContractLineId\":\"$GLINE\"}" "$OTOK"); PP=$(body "$PJ" | jget id)
post "/api/studio/payments/$PP/mark-paid" '{}' "$OTOK" >/dev/null                                                  # paid
post "/api/studio/students/$GS/payments" "{\"amount\":600,\"dueDate\":\"2020-01-01\",\"studentContractLineId\":\"$GLINE\"}" "$OTOK" >/dev/null          # overdue
# Sprint 27: 2 kredi → telafi TALEBİ akışı (rezerve + approve consume + retry başlığı) için headroom.
post "/api/studio/students/$GS/makeup-credits/grant" "{\"studentContractLineId\":\"$GLINE\",\"amount\":2,\"notes\":\"E2E demo\"}" "$OTOK" >/dev/null
ok "due-soon/paid/overdue ödeme + telafi kredisi"

echo "== 8) Bildirim üretimi (reminder trigger) =="
post /api/studio/notifications/run-reminders '{}' "$OTOK" >/dev/null
ok "Bildirimler üretildi (öğrenciye ders/ödeme hatırlatması)"

echo "== 9) İkinci stüdyo (askıya alınabilir test stüdyosu) =="
post /api/auth/register-studio "{\"studioName\":\"E2E Test Studio\",\"ownerFullName\":\"E2E Owner2\",\"email\":\"$OWNER2\",\"password\":\"$PW\"}" >/dev/null
ok "Test stüdyosu: E2E Test Studio (owner $OWNER2)"

# Sprint 16: seed kullanıcıları "yerleşik/doğrulanmış" sayılır (e-posta doğrulama banner'ı çıkmasın).
psql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\" LIKE 'e2e-%' AND \"EmailVerifiedAt\" IS NULL;"
ok "Seed kullanıcıları e-posta doğrulanmış işaretlendi"

echo
printf "\033[36mE2E SEED TAMAM ✅\033[0m  (tüm parolalar: %s)\n" "$PW"
echo "PlatformAdmin=$PADMIN  Owner=$OWNER  StudioAdmin=$ADMIN  Instructor=$INSTR  Student=$STUDENT"
