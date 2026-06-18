#!/usr/bin/env bash
# Pliaforma — Sprint 14 smoke (Paket Planı V2: ders grubu kapsamı, kapsam snapshot, booking eligibility +
# kullanılabilir hak/ExpectedStudentPackageId, RequiresPayment + Resolved, attendance consume kapsam,
# fiyat versiyonlama source-of-truth, yetki + tenant). Postgres container otomatik tespit (backdate gerekli).
# Kullanım: scripts/sprint14-smoke.sh [BASE]   (varsayılan http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jnum(){ grep -o "\"$1\":-\?[0-9.]*" | head -1 | sed "s/.*://"; }
# Fiyat numeric(12,2) → JSON'da "2000.00"; tamsayı kısmını al (karşılaştırma için).
pricei(){ echo "$1" | grep -o '"price":[0-9.]*' | head -1 | sed 's/.*://; s/\.[0-9]*$//'; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
pucode(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
putc(){ curl -s -o /dev/null -w "%{http_code}" -X PUT "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "$2"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
D=$(date -d "+3 days" +%Y-%m-%d); TODAY=$(date +%Y-%m-%d); FUT=$(date -d "+10 days" +%Y-%m-%d); PASTEFF=$(date -d "-1 day" +%Y-%m-%d)
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (backdate/stale cache gerekli)."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
backdate(){ sql "UPDATE \"ClassSessions\" SET \"StartAt\"='2020-01-01 10:00:00', \"EndAt\"='2020-01-01 11:00:00' WHERE \"Id\"='$1';" >/dev/null; }

# --- Stüdyo A + C(tenant) + hoca + 2 ders grubu ---
ATOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S14A $TS\",\"ownerFullName\":\"OA\",\"email\":\"s14a+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
CTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"S14C $TS\",\"ownerFullName\":\"OC\",\"email\":\"s14c+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
post /api/auth/register "{\"fullName\":\"i1\",\"email\":\"i1+$TS@pf.local\",\"password\":\"$PW\"}" >/dev/null
# Sprint 17: davet kabulü doğrulanmış e-posta ister + profil KABULDE oluşur (invite yanıtında değil).
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ curl -s "$BASE/api/studio/memberships" -H "Authorization: Bearer $ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
vrf "i1+$TS@pf.local"; RR=$(post /api/studio/memberships/invite "{\"email\":\"i1+$TS@pf.local\",\"role\":\"Instructor\",\"rate\":1}" "$ATOK"); MI=$(body "$RR" | jget membershipId); TMPI=$(login "i1+$TS@pf.local"); post "/api/members/$MI/accept" '{}' "$TMPI" >/dev/null; I1TOK=$(login "i1+$TS@pf.local"); I1P=$(ipid "i1+$TS@pf.local")
CT1=$(body "$(post /api/class-types "{\"name\":\"Mat\",\"equipment\":\"Mat\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
CT2=$(body "$(post /api/class-types "{\"name\":\"Reformer\",\"equipment\":\"Reformer\",\"capacityType\":\"Group\",\"maxCapacity\":5,\"level\":\"AllLevels\",\"purpose\":\"General\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
mksess(){ body "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$1\",\"startAt\":\"${D}T$2:00\",\"capacity\":5}" "$ATOK")" | jget id; }
guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }
mkactive(){ local g; g=$(guest "$1"); post "/api/studio/students/$g/convert-to-user" "{\"email\":\"$1+$TS@pf.local\",\"initialPassword\":\"$PW\"}" "$ATOK" >/dev/null; echo "$g"; }
sellcustom(){ body "$(post "/api/studio/students/$1/packages" "{\"name\":\"custom\",\"creditCount\":${2:-5}}" "$ATOK")"; }
sellplan(){ body "$(post "/api/studio/students/$1/packages" "{\"packagePlanId\":\"$2\"}" "$ATOK")"; }
pkgbal(){ curl -s "$BASE/api/studio/student-packages/$1" -H "Authorization: Bearer $ATOK" | jnum currentCreditBalance; }

echo "--- 1) Plan kapsamı (AllowAllClassTypes + PackagePlanClassType) ---"
RSC=$(post /api/studio/package-plans "{\"name\":\"Scoped $TS\",\"creditCount\":4,\"price\":1000,\"allowAllClassTypes\":false,\"classTypeIds\":[\"$CT1\"]}" "$ATOK")
expect "kapsamlı plan create → 200" 200 "$(code "$RSC")"; PSC=$(body "$RSC" | jget id)
expect "plan allowAllClassTypes=false" yes "$(has "$(body "$RSC")" '"allowAllClassTypes":false')"
expect "plan kapsamında CT1 var" yes "$(has "$(body "$RSC")" "$CT1")"
PALL=$(body "$(post /api/studio/package-plans "{\"name\":\"All $TS\",\"creditCount\":10,\"price\":2000}" "$ATOK")" | jget id)
expect "varsayılan plan allowAll=true" yes "$(has "$(curl -s "$BASE/api/studio/package-plans/$PALL" -H "Authorization: Bearer $ATOK")" '"allowAllClassTypes":true')"

echo "--- 2) StudentPackage kapsam snapshot ---"
SNAPG=$(guest "snap"); RSNAP=$(sellplan "$SNAPG" "$PSC"); PKSNAP=$(echo "$RSNAP" | jget id)
expect "satılan paket allowAllClassTypesSnapshot=false" yes "$(has "$RSNAP" '"allowAllClassTypesSnapshot":false')"
expect "satılan paket kapsamı CT1" yes "$(has "$RSNAP" "$CT1")"

echo "--- 3) Plan kapsamı değişince eski paket etkilenmez ---"
pucode "/api/studio/package-plans/$PSC" "{\"name\":\"Scoped $TS\",\"creditCount\":4,\"price\":1000,\"isActive\":true,\"allowAllClassTypes\":false,\"classTypeIds\":[\"$CT2\"]}" "$ATOK" >/dev/null
SNAP2=$(curl -s "$BASE/api/studio/student-packages/$PKSNAP" -H "Authorization: Bearer $ATOK")
expect "plan CT2'ye geçti ama eski paket hâlâ CT1 kapsamlı" yes "$(has "$SNAP2" "$CT1")"
expect "eski paket CT2 kapsamına geçmedi" no "$(has "$SNAP2" "$CT2")"

echo "--- 4) Self-booking: uygun CT paketi → 200 (+ExpectedStudentPackageId) ---"
SX=$(mkactive "sx"); SXTOK=$(login "sx+$TS@pf.local")
sellplan "$SX" "$PALL" >/dev/null   # AllowAll paket: her CT'yi kapsar
S_CT1=$(mksess "$CT1" "07:00")
RB=$(post "/api/student/class-sessions/$S_CT1/book" '{}' "$SXTOK")
expect "uygun paketle self-book → 200" 200 "$(code "$RB")"
expect "booking ExpectedStudentPackageId set" yes "$(has "$(body "$RB")" '"expectedStudentPackageId"')"

echo "--- 5) Wrong class type → 409 ---"
SW=$(mkactive "sw"); SWTOK=$(login "sw+$TS@pf.local")
# SW yalnızca CT1-kapsamlı paket alır (artık plan CT2; yeni satış CT2 snapshot olur). Bunun yerine elle CT1 kapsamlı plan:
PCT1=$(body "$(post /api/studio/package-plans "{\"name\":\"OnlyCT1 $TS\",\"creditCount\":5,\"price\":900,\"allowAllClassTypes\":false,\"classTypeIds\":[\"$CT1\"]}" "$ATOK")" | jget id)
sellplan "$SW" "$PCT1" >/dev/null
S_CT2=$(mksess "$CT2" "08:00")
expect "CT1 paketiyle CT2 self-book → 409" 409 "$(pcode "/api/student/class-sessions/$S_CT2/book" '{}' "$SWTOK")"
expect "SW CT1 self-book → 200" 200 "$(pcode "/api/student/class-sessions/$(mksess "$CT1" "09:00")/book" '{}' "$SWTOK")"

echo "--- 6) Paket yok → 409 (NoPackageAtAll) ---"
SN=$(mkactive "sn"); SNTOK=$(login "sn+$TS@pf.local")
RNO=$(post "/api/student/class-sessions/$(mksess "$CT1" "10:00")/book" '{}' "$SNTOK")  # 10:00
expect "paketsiz self-book → 409" 409 "$(code "$RNO")"

echo "--- 7) Kullanılabilir hak: 1 hak → 2. açık booking 409 → iptal sonra 200 ---"
SZ=$(mkactive "sz"); SZTOK=$(login "sz+$TS@pf.local")
sellcustom "$SZ" 1 >/dev/null            # 1 krediilik AllowAll paket
S1=$(mksess "$CT1" "11:00"); S2=$(mksess "$CT1" "12:00")
RB1=$(post "/api/student/class-sessions/$S1/book" '{}' "$SZTOK"); B1=$(body "$RB1" | jget id)
expect "1 hakla 1. self-book → 200" 200 "$(code "$RB1")"
expect "aynı pakete bağlı 2. açık self-book → 409 (hak yok)" 409 "$(pcode "/api/student/class-sessions/$S2/book" '{}' "$SZTOK")"
pcode "/api/student/bookings/$B1/cancel" '{}' "$SZTOK" >/dev/null
expect "1. booking iptal → hak available → 2. self-book 200" 200 "$(pcode "/api/student/class-sessions/$S2/book" '{}' "$SZTOK")"

echo "--- 8) Staff yetersiz hak → RequiresPayment=true; Instructor bypass edemez → 409 ---"
SP=$(guest "sp")   # paketsiz öğrenci
RSTAFF=$(post "/api/studio/class-sessions/$(mksess "$CT1" "13:00")/bookings" "{\"studentProfileId\":\"$SP\"}" "$ATOK")
expect "Owner paketsiz booking → 200" 200 "$(code "$RSTAFF")"
expect "Owner paketsiz booking requiresPayment=true" yes "$(has "$(body "$RSTAFF")" '"requiresPayment":true')"
GI=$(body "$(post /api/studio/students/guest "{\"fullName\":\"gi\",\"source\":\"Phone\"}" "$I1TOK")" | jget id)  # instructor oluşturur
SESS_I=$(mksess "$CT1" "14:00")
expect "Instructor paketsiz öğrenciyi kendi seansına → 409 (bypass yok)" 409 "$(pcode "/api/studio/class-sessions/$SESS_I/bookings" "{\"studentProfileId\":\"$GI\"}" "$I1TOK")"

echo "--- 9) Attendance consume ExpectedStudentPackageId paketinden düşer ---"
SC=$(mkactive "sc"); SCTOK=$(login "sc+$TS@pf.local")
RSC2=$(sellcustom "$SC" 5); PKSC=$(echo "$RSC2" | jget id)
SS=$(mksess "$CT1" "15:00"); BSC=$(body "$(post "/api/student/class-sessions/$SS/book" '{}' "$SCTOK")" | jget id)
backdate "$SS"
expect "consume (explicit'siz, expected paket) → 200" 200 "$(putc "/api/studio/class-sessions/$SS/attendance" "{\"items\":[{\"bookingId\":\"$BSC\",\"status\":\"Present\",\"consumePackageCredit\":true}]}" "$ATOK")"
expect "expected paketten düştü (bakiye 4)" 4 "$(pkgbal "$PKSC")"

echo "--- 10) Expected/explicit yanlış class type → consume 409 ---"
SM=$(mkactive "sm"); SMTOK=$(login "sm+$TS@pf.local")
PKM_CT1=$(echo "$(sellplan "$SM" "$PCT1")" | jget id)   # CT1 kapsamlı
sellcustom "$SM" 5 >/dev/null                            # AllowAll (CT2 self-book için)
S_M2=$(mksess "$CT2" "16:00"); BM=$(body "$(post "/api/student/class-sessions/$S_M2/book" '{}' "$SMTOK")" | jget id)
backdate "$S_M2"
expect "CT2 yoklamada CT1 paketi explicit → 409 (yanlış ders grubu)" 409 "$(putc "/api/studio/class-sessions/$S_M2/attendance" "{\"items\":[{\"bookingId\":\"$BM\",\"status\":\"Present\",\"consumePackageCredit\":true,\"studentPackageId\":\"$PKM_CT1\"}]}" "$ATOK")"

echo "--- 11) RequiresPayment → consume sonrası Resolved (RequiresPayment=false) ---"
SR=$(guest "sr"); SRS=$(mksess "$CT1" "17:00")
BR=$(body "$(post "/api/studio/class-sessions/$SRS/bookings" "{\"studentProfileId\":\"$SR\"}" "$ATOK")" | jget id)   # paketsiz → RequiresPayment
sellcustom "$SR" 3 >/dev/null   # sonradan paket sat
backdate "$SRS"
putc "/api/studio/class-sessions/$SRS/attendance" "{\"items\":[{\"bookingId\":\"$BR\",\"status\":\"Present\",\"consumePackageCredit\":true}]}" "$ATOK" >/dev/null
BLIST=$(curl -s "$BASE/api/studio/class-sessions/$SRS/bookings" -H "Authorization: Bearer $ATOK")
expect "consume sonrası RequiresPayment=false" yes "$(has "$BLIST" '"requiresPayment":false')"
expect "consume sonrası PackageEligibilityStatus=Resolved" yes "$(has "$BLIST" '"packageEligibilityStatus":"Resolved"')"

echo "--- 12) Fiyat versiyonlama (source of truth = versiyon tablosu) ---"
PP=$(body "$(post /api/studio/package-plans "{\"name\":\"Price $TS\",\"creditCount\":8,\"price\":2000}" "$ATOK")" | jget id)
GP=$(guest "gp"); RB2000=$(sellplan "$GP" "$PP")
expect "bugün effective 2000 → satış snapshot 2000" 2000 "$(pricei "$RB2000")"
# Gelecek 2500 ekle → bugün hâlâ 2000
pcode "/api/studio/package-plans/$PP/price-versions" "{\"price\":2500,\"effectiveFrom\":\"$FUT\"}" "$ATOK" >/dev/null
GP2=$(guest "gp2"); expect "gelecek 2500 varken bugün satış hâlâ 2000" 2000 "$(pricei "$(sellplan "$GP2" "$PP")")"
expect "plan DTO nextEffectivePrice=2500" yes "$(has "$(curl -s "$BASE/api/studio/package-plans/$PP" -H "Authorization: Bearer $ATOK")" '"nextEffectivePrice":2500')"
# Bugünden geçerli 2500 versiyonu (bugünkü versiyonu günceller) → effective 2500
pcode "/api/studio/package-plans/$PP/price-versions" "{\"price\":2500,\"effectiveFrom\":\"$TODAY\"}" "$ATOK" >/dev/null
GP3=$(guest "gp3"); expect "bugün geçerli 2500 effective → satış 2500" 2500 "$(pricei "$(sellplan "$GP3" "$PP")")"
# Stale cache: PackagePlans.Price'ı bozuk değere çek → satış yine computed (2500)
sql "UPDATE \"PackagePlans\" SET \"Price\"=99999 WHERE \"Id\"='$PP';" >/dev/null
GP4=$(guest "gp4"); expect "stale cache (99999) olsa bile satış computed (2500)" 2500 "$(pricei "$(sellplan "$GP4" "$PP")")"
# Mevcut paket fiyatı değişmez
expect "eski paket fiyatı plan değişiminden etkilenmedi (2000)" 2000 "$(pricei "$(curl -s "$BASE/api/studio/student-packages/$(echo "$RB2000" | jget id)" -H "Authorization: Bearer $ATOK")")"

echo "--- 13) Yetki + Tenant ---"
expect "Instructor price-version ekle → 403" 403 "$(pcode "/api/studio/package-plans/$PP/price-versions" "{\"price\":1,\"effectiveFrom\":\"$TODAY\"}" "$I1TOK")"
expect "Instructor package-plans list → 403" 403 "$(gcode /api/studio/package-plans "$I1TOK")"
expect "C, A planını güncelle → 404" 404 "$(pucode "/api/studio/package-plans/$PP" "{\"name\":\"hack\",\"creditCount\":1,\"price\":0,\"isActive\":true,\"allowAllClassTypes\":true,\"classTypeIds\":[]}" "$CTOK")"
expect "C, A planına price-version → 404" 404 "$(pcode "/api/studio/package-plans/$PP/price-versions" "{\"price\":1,\"effectiveFrom\":\"$TODAY\"}" "$CTOK")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 14 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 14 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
