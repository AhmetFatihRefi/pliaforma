#!/usr/bin/env bash
# Pliaforma — Sprint 15 smoke (self-service kayıt & stüdyoya katılım isteği: JoinCode, rol başvuru,
# onayda profil oluşumu, duplicate/pending kuralları, yetki + tenant). Postgres container gerekli (profil/uid).
# Kullanım: scripts/sprint15-smoke.sh [BASE]   (varsayılan http://localhost:5132)
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
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container bulunamadı (uid/profil kontrolü gerekli)."; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
uid(){ sql "SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='$1';"; }
scount(){ sql "SELECT count(*) FROM \"StudentProfiles\" WHERE \"UserId\"='$1' AND \"StudioId\"='$2' AND \"IsDeleted\"=false;"; }
icount(){ sql "SELECT count(*) FROM \"InstructorProfiles\" WHERE \"UserId\"='$1' AND \"StudioId\"='$2' AND \"IsDeleted\"=false;"; }

regstudio(){ body "$(post /api/auth/register-studio "{\"studioName\":\"$1\",\"ownerFullName\":\"O\",\"email\":\"$2\",\"password\":\"$PW\"}")" | jget accessToken; }
# Sprint 16: apply artık e-posta doğrulaması ister → test applicant'ları register sonrası DB'de verified yapılır.
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
reg(){ local r; r=$(post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$2\",\"password\":\"$PW\"}"); vrf "$2"; echo "$r"; }   # full response (+verify)
regtok(){ body "$(reg "$1" "$2")" | jget accessToken; }
joincode(){ curl -s "$BASE/api/studio/join-code" -H "Authorization: Bearer $1" | jget joinCode; }
joincodef(){ curl -s "$BASE/api/studio/join-code" -H "Authorization: Bearer $1" | jget formattedJoinCode; }
apply(){ post /api/members/apply "{\"joinCode\":\"$1\",\"role\":\"$2\"}" "$3"; }   # full response

# --- Stüdyo A + B (tenant) + StudioAdmin ---
AEMAIL="s15a+$TS@pf.local"; ATOK=$(regstudio "S15A $TS" "$AEMAIL")
BEMAIL="s15b+$TS@pf.local"; BTOK=$(regstudio "S15B $TS" "$BEMAIL")
SID_A=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"OwnerUserId\"='$(uid "$AEMAIL")';")
JCA=$(joincode "$ATOK"); JCA_F=$(joincodef "$ATOK"); JCB=$(joincode "$BTOK")

# StudioAdmin (adminU): register → Owner davet eder → adminU kabul eder
ADMU="s15adm+$TS@pf.local"; ADMTOK=$(regtok "Adm" "$ADMU")
MIA=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$ADMU\",\"role\":\"StudioAdmin\"}" "$ATOK")" | jget membershipId)
pcode "/api/members/$MIA/accept" '{}' "$ADMTOK" >/dev/null
ADMTOK=$(body "$(post /api/auth/login "{\"email\":\"$ADMU\",\"password\":\"$PW\"}")" | jget accessToken)  # studio context için re-login

echo "--- 1) Açık kayıt + duplicate ---"
R=$(reg "Stu One" "stu1+$TS@pf.local"); expect "açık register → 200" 200 "$(code "$R")"
expect "aynı email ile register → 409" 409 "$(code "$(reg "Stu One" "stu1+$TS@pf.local")")"
STU1TOK=$(body "$R" | jget accessToken); STU1ID=$(uid "stu1+$TS@pf.local")
INS1TOK=$(regtok "Ins One" "ins1+$TS@pf.local"); INS1ID=$(uid "ins1+$TS@pf.local")

echo "--- 2) JoinCode geçerlilik + generic 404 ---"
expect "Owner join-code görür → 200" 200 "$(gcode /api/studio/join-code "$ATOK")"
expect "geçersiz JoinCode → 404 (generic)" 404 "$(code "$(apply "ZZZZZZZZZZ" "Student" "$STU1TOK")")"

echo "--- 3) Student başvurusu (Pending, profil YOK) ---"
RS=$(apply "$JCA" "Student" "$STU1TOK"); expect "Student başvuru → 200" 200 "$(code "$RS")"
MIS=$(body "$RS" | jget membershipId)
expect "başvuru sonrası StudentProfile YOK (0)" 0 "$(scount "$STU1ID" "$SID_A")"
expect "aynı stüdyoya 2. Pending başvuru → 409" 409 "$(code "$(apply "$JCA" "Student" "$STU1TOK")")"

echo "--- 4) Sprint 17: Instructor JoinCode KAPALI + ikinci kullanıcı Student (tireli kod normalize) ---"
expect "Instructor JoinCode apply → 400 (hoca davetle katılır)" 400 "$(code "$(apply "$JCA" "Instructor" "$INS1TOK")")"
# INS1 kullanıcısı izolasyon/normalize testi için A'ya ÖĞRENCİ başvurusu yapar (tireli kod normalize).
RI=$(apply "$JCA_F" "Student" "$INS1TOK"); expect "tireli kod (normalize) ile Student başvuru → 200" 200 "$(code "$RI")"
MII=$(body "$RI" | jget membershipId)
expect "başvuru sonrası StudentProfile YOK (0)" 0 "$(scount "$INS1ID" "$SID_A")"

echo "--- 5) Sınırlı roller ---"
expect "Owner rolüne başvuru → 400" 400 "$(code "$(apply "$JCA" "Owner" "$INS1TOK")")"
expect "StudioAdmin rolüne başvuru → 400" 400 "$(code "$(apply "$JCA" "StudioAdmin" "$INS1TOK")")"

echo "--- 6) Stüdyo join-requests yetkisi ---"
JR=$(curl -s "$BASE/api/studio/join-requests" -H "Authorization: Bearer $ATOK")
expect "Owner join-requests → 200" 200 "$(gcode /api/studio/join-requests "$ATOK")"
expect "join-requests başvuranı içerir" yes "$(has "$JR" "stu1+$TS@pf.local")"
expect "StudioAdmin join-requests → 200" 200 "$(gcode /api/studio/join-requests "$ADMTOK")"
expect "applicant (Student) join-requests → 403" 403 "$(gcode /api/studio/join-requests "$STU1TOK")"
expect "applicant (Instructor) join-requests → 403" 403 "$(gcode /api/studio/join-requests "$INS1TOK")"
expect "B Owner, A join-request'i (cross-tenant) görmez" no "$(has "$(curl -s "$BASE/api/studio/join-requests" -H "Authorization: Bearer $BTOK")" "stu1+$TS@pf.local")"

echo "--- 7) my-join-requests yalnız kendi ---"
MJR=$(curl -s "$BASE/api/members/my-join-requests" -H "Authorization: Bearer $STU1TOK")
expect "kendi başvurusunu görür" yes "$(has "$MJR" "$MIS")"
expect "başkasının (Instructor) başvurusunu görmez" no "$(has "$MJR" "$MII")"

echo "--- 8) Kabul → profil oluşur (Owner: Student) ---"
expect "Owner Student başvurusunu kabul → 200" 200 "$(pcode "/api/members/$MIS/accept" '{}' "$ATOK")"
expect "kabul sonrası StudentProfile oluştu (1)" 1 "$(scount "$STU1ID" "$SID_A")"
expect "Active üyeyken tekrar başvuru → 409" 409 "$(code "$(apply "$JCA" "Student" "$STU1TOK")")"

echo "--- 9) Kabul → profil oluşur (StudioAdmin: ikinci Student) ---"
expect "StudioAdmin Student başvurusunu kabul → 200" 200 "$(pcode "/api/members/$MII/accept" '{}' "$ADMTOK")"
expect "kabul sonrası StudentProfile oluştu (1)" 1 "$(scount "$INS1ID" "$SID_A")"

echo "--- 10) Reject → profil oluşmaz + Rejected sonrası yeniden başvuru ---"
RJTOK=$(regtok "Rej" "rej+$TS@pf.local"); RJID=$(uid "rej+$TS@pf.local")
MIR=$(body "$(apply "$JCA" "Student" "$RJTOK")" | jget membershipId)
expect "Owner başvuruyu reddet → 200" 200 "$(pcode "/api/members/$MIR/reject" '{}' "$ATOK")"
expect "reddedilen başvuruda StudentProfile YOK (0)" 0 "$(scount "$RJID" "$SID_A")"
expect "Rejected sonrası yeniden başvuru → 200" 200 "$(code "$(apply "$JCA" "Student" "$RJTOK")")"

echo "--- 11) JoinCode regenerate ---"
expect "Instructor regenerate → 403" 403 "$(pcode /api/studio/join-code/regenerate '{}' "$INS1TOK")"
NEW=$(body "$(post /api/studio/join-code/regenerate '{}' "$ATOK")" | jget joinCode)
NWTOK=$(regtok "New" "new+$TS@pf.local")
expect "regenerate sonrası ESKİ kod → 404" 404 "$(code "$(apply "$JCA" "Student" "$NWTOK")")"
expect "regenerate sonrası YENİ kod → 200" 200 "$(code "$(apply "$NEW" "Student" "$NWTOK")")"

echo
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 15 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 15 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
