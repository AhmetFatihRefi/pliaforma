#!/usr/bin/env bash
# Pliaforma — Sprint 17 smoke (Davetlerim, hoca davet akışı, stüdyosuz kullanıcı alanı):
#  - Hoca JoinCode self-apply KAPALI (400); öğrenci açık (200).
#  - Stüdyo davet → Pending (profil YOK invite anında); MembershipInvite bildirimi (dedup).
#  - Davet kabulü doğrulanmış e-posta ister (403 → vrf → 200 + InstructorProfile kabulde oluşur).
#  - Reddetme → profil oluşmaz. Üyeli kullanıcı yeni davet (cross-tenant bildirim + my-invitations).
#  - Tenant/recipient izolasyonu.
# Postgres container gerekli (uid/profil/notification/verify). Kullanım: scripts/sprint17-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" -H "Authorization: Bearer $2"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' -H "Authorization: Bearer $3" -d "${2:-{\}}"; }
code(){ echo "$1" | tail -1; }; body(){ echo "$1" | sed '$d'; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }

PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (uid/profil/notification kontrolü gerekli)."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
uid(){ sql "SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='$1';"; }
icount(){ sql "SELECT count(*) FROM \"InstructorProfiles\" WHERE \"UserId\"='$1' AND \"StudioId\"='$2' AND \"IsDeleted\"=false;"; }
scount(){ sql "SELECT count(*) FROM \"StudentProfiles\" WHERE \"UserId\"='$1' AND \"StudioId\"='$2' AND \"IsDeleted\"=false;"; }
nicount(){ sql "SELECT count(*) FROM \"Notifications\" WHERE \"RecipientUserId\"='$1' AND \"Type\"='MembershipInvite' AND \"IsDeleted\"=false;"; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }

regstudio(){ body "$(post /api/auth/register-studio "{\"studioName\":\"$1\",\"ownerFullName\":\"O\",\"email\":\"$2\",\"password\":\"$PW\"}")" | jget accessToken; }
reg(){ post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$2\",\"password\":\"$PW\"}" >/dev/null; }      # UNVERIFIED bırakır
regv(){ reg "$1" "$2"; vrf "$2"; }                                                                            # register + verify
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
joincode(){ get /api/studio/join-code "$1" | jget joinCode; }
apply(){ post /api/members/apply "{\"joinCode\":\"$1\",\"role\":\"$2\"}" "$3"; }
invite(){ post /api/studio/memberships/invite "{\"email\":\"$1\",\"role\":\"$2\"}" "$3"; }
myinv(){ get /api/members/my-invitations "$1"; }

# --- Stüdyo A + B (tenant) ---
AEMAIL="s17a+$TS@pf.local"; ATOK=$(regstudio "S17A $TS" "$AEMAIL"); SID_A=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"OwnerUserId\"='$(uid "$AEMAIL")';")
BEMAIL="s17b+$TS@pf.local"; BTOK=$(regstudio "S17B $TS" "$BEMAIL"); SID_B=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"OwnerUserId\"='$(uid "$BEMAIL")';")
JCA=$(joincode "$ATOK")

echo "--- 0) Kayıt niyeti (RegistrationIntent) round-trip ---"
IE0="intent17+$TS@pf.local"
RI0=$(post /api/auth/register "{\"fullName\":\"Hoca Intent\",\"email\":\"$IE0\",\"password\":\"$PW\",\"intent\":\"Instructor\"}")
expect "register intent=Instructor → 200" 200 "$(code "$RI0")"
expect "auth yanıtı registrationIntent=Instructor" yes "$(has "$(body "$RI0")" '"registrationIntent":"Instructor"')"

echo "--- 1) Hoca JoinCode self-apply KAPALI, Öğrenci açık ---"
SE="stu17+$TS@pf.local"; regv "Stu" "$SE"; STOK=$(login "$SE")
expect "Instructor JoinCode apply → 400" 400 "$(code "$(apply "$JCA" "Instructor" "$STOK")")"
expect "Student JoinCode apply → 200" 200 "$(code "$(apply "$JCA" "Student" "$STOK")")"

echo "--- 1b) Rev2: DOĞRULANMAMIŞ öğrenci apply yapabilir; aktivasyon (accept) gate'i e-posta ister ---"
USE="ustu17+$TS@pf.local"; reg "UStu" "$USE"; USID=$(uid "$USE"); USTOK=$(login "$USE")   # UNVERIFIED
RUA=$(apply "$JCA" "Student" "$USTOK"); expect "unverified Student apply → 200 (Pending)" 200 "$(code "$RUA")"
USMID=$(body "$RUA" | jget membershipId)
expect "apply sonrası StudentProfile YOK (0)" 0 "$(scount "$USID" "$SID_A")"
expect "owner unverified öğrenciyi accept → 403 (aktivasyon gate'i)" 403 "$(pcode "/api/members/$USMID/accept" '{}' "$ATOK")"
vrf "$USE"
expect "doğrulama sonrası owner accept → 200" 200 "$(pcode "/api/members/$USMID/accept" '{}' "$ATOK")"
expect "kabul sonrası StudentProfile oluştu (1)" 1 "$(scount "$USID" "$SID_A")"

echo "--- 2) Stüdyo hoca davet eder → Pending + profil YOK + bildirim ---"
IE="hoca17+$TS@pf.local"; reg "Hoca" "$IE"; IID=$(uid "$IE")   # UNVERIFIED (403 testi için)
RINV=$(invite "$IE" "Instructor" "$ATOK"); expect "Owner hoca davet → 200" 200 "$(code "$RINV")"
MIV=$(body "$RINV" | jget membershipId)
expect "invite yanıtında InstructorProfile YOK (null)" "" "$(body "$RINV" | jget instructorProfileId)"
expect "invite anında InstructorProfile DB'de YOK (0)" 0 "$(icount "$IID" "$SID_A")"
expect "MembershipInvite bildirimi oluştu (1)" 1 "$(nicount "$IID")"

echo "--- 3) Davet edilen kullanıcı davetini görür (my-invitations) ---"
ITOK=$(login "$IE")
expect "davet edilen my-invitations → 200" 200 "$(gcode /api/members/my-invitations "$ITOK")"
expect "my-invitations daveti içerir" yes "$(has "$(myinv "$ITOK")" "$MIV")"

echo "--- 4) Duplicate davet → 409 + bildirim tekilleşir (hâlâ 1) ---"
expect "aynı kullanıcıyı tekrar davet → 409" 409 "$(code "$(invite "$IE" "Instructor" "$ATOK")")"
expect "duplicate notification YOK (hâlâ 1)" 1 "$(nicount "$IID")"

echo "--- 5) Doğrulanmamış kabul → 403 ---"
expect "unverified accept → 403" 403 "$(pcode "/api/members/$MIV/accept" '{}' "$ITOK")"
expect "403 sonrası membership Pending (profil yok)" 0 "$(icount "$IID" "$SID_A")"

echo "--- 6) Doğrulandıktan sonra kabul → Active + InstructorProfile (kabulde) ---"
vrf "$IE"
expect "verified accept → 200" 200 "$(pcode "/api/members/$MIV/accept" '{}' "$ITOK")"
expect "kabul sonrası InstructorProfile oluştu (1)" 1 "$(icount "$IID" "$SID_A")"

echo "--- 7) Reddetme → Rejected, profil oluşmaz ---"
JE="hocard17+$TS@pf.local"; regv "HocaR" "$JE"; JID=$(uid "$JE"); JTOK=$(login "$JE")
MIR=$(body "$(invite "$JE" "Instructor" "$ATOK")" | jget membershipId)
expect "davet reddet → 200" 200 "$(pcode "/api/members/$MIR/reject" '{}' "$JTOK")"
expect "reddedilen davette InstructorProfile YOK (0)" 0 "$(icount "$JID" "$SID_A")"

echo "--- 8) Üyeli kullanıcı (A hocası) B'den davet alır → cross-tenant bildirim + my-invitations ---"
RINVB=$(invite "$IE" "Instructor" "$BTOK"); expect "B Owner A-hocasını davet → 200" 200 "$(code "$RINVB")"
MIVB=$(body "$RINVB" | jget membershipId)
expect "B daveti A-hocasının my-invitations'ında" yes "$(has "$(myinv "$ITOK")" "$MIVB")"
# Aktif stüdyosu A iken, B daveti bildirimi yine de çanda görünür (MembershipInvite cross-tenant istisnası).
ITOK=$(login "$IE")  # aktif bağlam A
expect "B-invite bildirimi A bağlamında çanda görünür" yes "$(has "$(get /api/notifications "$ITOK")" "Stüdyo daveti")"
expect "B daveti bildirimi DB'de (toplam 2 MembershipInvite)" 2 "$(nicount "$IID")"

echo "--- 9) Üyeli kullanıcı B davetini kabul eder → ikinci stüdyoya üye + profil ---"
vrf "$IE"
expect "B davetini kabul → 200" 200 "$(pcode "/api/members/$MIVB/accept" '{}' "$ITOK")"
expect "B'de InstructorProfile oluştu (1)" 1 "$(icount "$IID" "$SID_B")"

echo "--- 10) Tenant/recipient izolasyonu ---"
expect "başka kullanıcı (öğrenci) B davetini görmez" no "$(has "$(myinv "$STOK")" "$MIVB")"
expect "öğrenci my-invitations boş/davetsiz (MIV yok)" no "$(has "$(myinv "$STOK")" "$MIV")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 17 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 17 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
