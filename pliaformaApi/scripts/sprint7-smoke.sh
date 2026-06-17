#!/usr/bin/env bash
# Pliaforma — Sprint 7 smoke (paket planları, öğrenci paketi, ledger, attendance→paket düşüm, ödeme,
# yetki + tenant). Kullanım: scripts/sprint7-smoke.sh [BASE]  (vars. http://localhost:5132)
# Not: attendance "başlamış seans" ister; create geçmişi reddettiğinden seans ileri açılıp psql ile
# geçmişe çekilir. Postgres container otomatik tespit edilir (native/compose).
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":-\?[0-9]*" | head -1 | sed "s/.*://"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
putcode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
pucode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
D=$(date -d "+3 days" +%Y-%m-%d)
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (backdate gerekli)."; exit 2; }
backdate(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"ClassSessions\" SET \"StartAt\"='2020-01-01 10:00:00', \"EndAt\"='2020-01-01 11:00:00' WHERE \"Id\"='$1';" >/dev/null 2>&1; }
expirepkg(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "UPDATE \"StudentPackages\" SET \"ValidUntil\"='2020-01-01' WHERE \"Id\"='$1';" >/dev/null 2>&1; }
pkgeff(){ curl -s "$BASE/api/studio/student-packages/$1" -H "Authorization: Bearer $ATOK" | jget effectiveStatus; }

# --- Stüdyolar + hoca + grup ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S7A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s7a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S7C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s7c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
post /api/auth/register "{\"fullName\":\"i1\",\"email\":\"i1+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
RR=$(post /api/studio/memberships/invite "{\"email\":\"i1+$TS@pf.local\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); MI=$(body "$RR" | jget membershipId); I1TOK=$(login "i1+$TS@pf.local"); post "/api/members/$MI/accept" '{}' "$I1TOK" >/dev/null; I1P=$(body "$RR" | jget instructorProfileId)
CT=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"${D}T$1:00\",\"capacity\":5}" "$ATOK")" | jget id; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
sbook(){ body "$(post "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK")" | jget id; }
sellp(){ body "$(post "/api/studio/students/$1/packages" "{\"packagePlanId\":\"$2\"}" "$ATOK")" | jget id; }
pkgbal(){ curl -s "$BASE/api/studio/student-packages/$1" -H "Authorization: Bearer $ATOK" | jnum currentCreditBalance; }
markc(){ putcode "/api/studio/class-sessions/$1/attendance" "{\"items\":[{\"bookingId\":\"$2\",\"status\":\"Present\",\"consumePackageCredit\":true${3:+,\"studentPackageId\":\"$3\"}}]}" "${4:-$ATOK}"; }

echo "--- PackagePlan CRUD ---"
R=$(post /api/studio/package-plans "{\"name\":\"4 Ders $TS\",\"creditCount\":4,\"price\":1000,\"validityDays\":30}" "$ATOK")
expect "plan create → 200" 200 "$(code "$R")"; PL=$(body "$R" | jget id)
PL1=$(body "$(post /api/studio/package-plans "{\"name\":\"1 Ders $TS\",\"creditCount\":1,\"price\":300}" "$ATOK")" | jget id)
expect "plan update → 200" 200 "$(pucode "/api/studio/package-plans/$PL" "{\"name\":\"4 Ders v2 $TS\",\"creditCount\":4,\"price\":1100,\"isActive\":true}" "$ATOK")"
PLX=$(body "$(post /api/studio/package-plans "{\"name\":\"Sil $TS\",\"creditCount\":2,\"price\":1}" "$ATOK")" | jget id)
expect "plan delete → 200" 200 "$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BASE/api/studio/package-plans/$PLX" -H "Authorization: Bearer $ATOK")"

echo "--- Paket sat + bakiye ---"
GA=$(guest "GA"); R=$(post "/api/studio/students/$GA/packages" "{\"packagePlanId\":\"$PL\"}" "$ATOK")
expect "paket sat → 200" 200 "$(code "$R")"; PKA=$(body "$R" | jget id)
expect "satılan paket bakiyesi 4" 4 "$(body "$R" | jnum currentCreditBalance)"

echo "--- Attendance → paket düşümü + idempotency ---"
PS=$(mksess "10:00"); BA=$(sbook "$PS" "$GA"); backdate "$PS"
expect "attendance consume → 200" 200 "$(markc "$PS" "$BA")"
expect "consume sonrası bakiye 3" 3 "$(pkgbal "$PKA")"
markc "$PS" "$BA" >/dev/null
expect "aynı attendance ikinci consume → bakiye hâlâ 3" 3 "$(pkgbal "$PKA")"

echo "--- studentPackageId ile doğru paketten düşüm ---"
GB=$(guest "GB"); PKB1=$(sellp "$GB" "$PL"); PKB2=$(sellp "$GB" "$PL")
PS2=$(mksess "11:00"); BB=$(sbook "$PS2" "$GB"); backdate "$PS2"
expect "belirtilen paketten consume → 200" 200 "$(markc "$PS2" "$BB" "$PKB2")"
expect "PKB2 bakiye 3" 3 "$(pkgbal "$PKB2")"
expect "PKB1 bakiye değişmedi (4)" 4 "$(pkgbal "$PKB1")"

echo "--- Bakiye 0 → 409 ---"
GC=$(guest "GC"); PKC=$(sellp "$GC" "$PL1")
PS3=$(mksess "12:00"); BC=$(sbook "$PS3" "$GC"); backdate "$PS3"
expect "1 kredilik paket consume → 200" 200 "$(markc "$PS3" "$BC")"
expect "tükenen paket bakiye 0" 0 "$(pkgbal "$PKC")"
PS3b=$(mksess "13:00"); BC2=$(sbook "$PS3b" "$GC"); backdate "$PS3b"
expect "kullanılabilir paket yok → consume 409" 409 "$(markc "$PS3b" "$BC2")"

echo "--- İptal paketten consume → 409 ---"
GD=$(guest "GD"); PKD=$(sellp "$GD" "$PL")
pcode "/api/studio/student-packages/$PKD/cancel" '{}' "$ATOK" >/dev/null
PS5=$(mksess "14:00"); BD=$(sbook "$PS5" "$GD"); backdate "$PS5"
expect "iptal paket seçilerek consume → 409" 409 "$(markc "$PS5" "$BD" "$PKD")"

echo "--- (A) Expired paket consume engeli (explicit) ---"
GE=$(guest "GE"); PKE=$(sellp "$GE" "$PL"); expirepkg "$PKE"
PSE=$(mksess "16:00"); BE=$(sbook "$PSE" "$GE"); backdate "$PSE"
expect "expired paket seçilerek consume → 409" 409 "$(markc "$PSE" "$BE" "$PKE")"
expect "expired consume sonrası bakiye değişmedi (4, rollback)" 4 "$(pkgbal "$PKE")"

echo "--- (B) Auto selection expired paketi atlar ---"
GF=$(guest "GF"); P1=$(sellp "$GF" "$PL"); P2=$(sellp "$GF" "$PL"); expirepkg "$P1"
PSF=$(mksess "17:00"); BFk=$(sbook "$PSF" "$GF"); backdate "$PSF"
expect "auto consume (expired atlanır) → 200" 200 "$(markc "$PSF" "$BFk")"
expect "geçerli P2 bakiye 3" 3 "$(pkgbal "$P2")"
expect "expired P1 bakiye değişmedi (4)" 4 "$(pkgbal "$P1")"

echo "--- (C) Sadece expired paket varsa → 409 ---"
GG=$(guest "GG"); PKG=$(sellp "$GG" "$PL"); expirepkg "$PKG"
PSG=$(mksess "18:00"); BG=$(sbook "$PSG" "$GG"); backdate "$PSG"
expect "yalnız expired paket → auto consume 409" 409 "$(markc "$PSG" "$BG")"

echo "--- (D) DTO effectiveStatus ---"
expect "expired paket effectiveStatus=Expired" Expired "$(pkgeff "$PKE")"
expect "geçerli paket effectiveStatus=Active" Active "$(pkgeff "$P2")"

echo "--- Manuel adjust ---"
expect "adjust +2 → 200" 200 "$(pcode "/api/studio/student-packages/$PKA/adjust" "{\"amount\":2,\"notes\":\"x\"}" "$ATOK")"
expect "adjust sonrası bakiye 5" 5 "$(pkgbal "$PKA")"
expect "negatif yapan adjust → 409" 409 "$(pcode "/api/studio/student-packages/$PKA/adjust" "{\"amount\":-99}" "$ATOK")"

echo "--- Ödeme akışı ---"
R=$(post "/api/studio/students/$GA/payments" "{\"amount\":500,\"dueDate\":\"$D\",\"method\":\"Cash\"}" "$ATOK")
expect "ödeme create → 200" 200 "$(code "$R")"; PAY=$(body "$R" | jget id)
expect "ödeme mark-paid → 200" 200 "$(pcode "/api/studio/payments/$PAY/mark-paid" '{}' "$ATOK")"
R2=$(post "/api/studio/students/$GA/payments" "{\"amount\":750,\"dueDate\":\"2020-01-01\"}" "$ATOK")
PAY2=$(body "$R2" | jget id)
expect "vadesi geçmiş pending → Overdue (dinamik)" Overdue "$(body "$R2" | jget status)"
OL=$(curl -s "$BASE/api/studio/payments?status=Overdue" -H "Authorization: Bearer $ATOK")
expect "Overdue filtresi PAY2'yi listeler" yes "$(has "$OL" "$PAY2")"
expect "ödeme cancel → 200" 200 "$(pcode "/api/studio/payments/$PAY2/cancel" '{}' "$ATOK")"

echo "--- Student self (GA convert) ---"
post "/api/studio/students/$GA/convert-to-user" "{\"email\":\"ga+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null
GATOK=$(login "ga+$TS@pf.local")
expect "student kendi paketleri → 200" 200 "$(gcode /api/student/packages "$GATOK")"
expect "student paketinde kayıt var" yes "$(has "$(curl -s "$BASE/api/student/packages" -H "Authorization: Bearer $GATOK")" '"currentCreditBalance"')"
expect "student kendi ödemeleri → 200" 200 "$(gcode /api/student/payments "$GATOK")"

echo "--- Yetki: Instructor / Student yönetim uçları ---"
expect "Instructor package-plans → 403" 403 "$(gcode /api/studio/package-plans "$I1TOK")"
expect "Instructor payments → 403" 403 "$(gcode /api/studio/payments "$I1TOK")"
expect "Student package-plans → 403" 403 "$(gcode /api/studio/package-plans "$GATOK")"
expect "Student paket sat → 403" 403 "$(pcode "/api/studio/students/$GA/packages" "{\"packagePlanId\":\"$PL\"}" "$GATOK")"
# Instructor attendance'ta paket düşüremez
PSI=$(mksess "15:00"); BAI=$(sbook "$PSI" "$GB"); backdate "$PSI"
expect "Instructor consumePackageCredit → 403" 403 "$(markc "$PSI" "$BAI" "" "$I1TOK")"

echo "--- Tenant izolasyonu (C) ---"
expect "C, A planını güncelle → 404" 404 "$(pucode "/api/studio/package-plans/$PL" "{\"name\":\"hack\",\"creditCount\":1,\"price\":0,\"isActive\":true}" "$CTOK")"
expect "C, A öğrencisine paket → 404" 404 "$(pcode "/api/studio/students/$GA/packages" "{\"packagePlanId\":\"$PL\"}" "$CTOK")"
expect "C, A paketini get → 404" 404 "$(gcode "/api/studio/student-packages/$PKA" "$CTOK")"
expect "C, A ödemesini güncelle → 404" 404 "$(pucode "/api/studio/payments/$PAY" "{\"amount\":1,\"dueDate\":\"$D\"}" "$CTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 7 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 7 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
