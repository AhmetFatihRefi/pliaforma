#!/usr/bin/env bash
# Pliaforma — Sprint 43 smoke (Student Booking Move): kaynaktan seçili öğrenciyi hedef derse / yeni boşluğa taşı.
# Transaction + BookedCount + ders hakkı nötrlüğü + guard'lar (kapasite/duplicate/çakışma/yoklama/Regular/geçmiş) + rol + audit.
# Postgres + node gerekli. Kullanım: scripts/sprint43-smoke.sh [BASE]
set -u
BASE="${1:-http://localhost:5132}"
TS=$(date +%s); PW="Parola123!"; FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
expect(){ [ "$2" = "$3" ] && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen $3)"; }
expectn(){ awk "BEGIN{exit !((${2:-x})==(${3:-y}))}" 2>/dev/null && ok "$1 ($3)" || bad "$1 (beklenen $2, gelen ${3:-bos})"; }
has(){ echo "$1" | grep -q "$2" && echo yes || echo no; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('ERR')}"; }
post(){ curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "${2:-{\}}"; }
gcode(){ curl -s -o /dev/null -w "%{http_code}" "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
gbody(){ curl -s "$BASE$1" ${2:+-H "Authorization: Bearer $2"}; }
code(){ echo "$1" | tail -1; }; bdy(){ echo "$1" | sed '$d'; }
login(){ bdy "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok"; exit 2; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null | tr -d '\r'; }
vrf(){ sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$1' AND \"EmailVerifiedAt\" IS NULL;" >/dev/null; }
ipid(){ gbody "/api/studio/memberships" "$ATOK" | tr '{' '\n' | grep "\"email\":\"$1\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/'; }
guest(){ bdy "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"Phone\"}" "$ATOK")" | jget id; }

ATOK=$(bdy "$(post /api/auth/register-studio "{\"studioName\":\"S43 $TS\",\"ownerFullName\":\"O\",\"email\":\"o43+$TS@pf.local\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$ATOK" ] && { echo "register-studio FAIL"; exit 1; }
invinstr(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(bdy "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Instructor\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; ipid "$e"; }
mkstudent_user(){ local e="$1+$TS@pf.local"; post /api/auth/register "{\"fullName\":\"$1\",\"email\":\"$e\",\"password\":\"$PW\"}" >/dev/null; vrf "$e"; local m; m=$(bdy "$(post /api/studio/memberships/invite "{\"email\":\"$e\",\"role\":\"Student\"}" "$ATOK")" | jget membershipId); local t; t=$(login "$e"); post "/api/members/$m/accept" '{}' "$t" >/dev/null; login "$e"; }
I1P=$(invinstr i1); I1TOK=$(login "i1+$TS@pf.local"); I2P=$(invinstr i2)
CT=$(bdy "$(post /api/class-types "{\"name\":\"Mat\",\"defaultDurationMinutes\":60}" "$ATOK")" | jget id)
TODAY=$(date +%F); END=$(date -d "+60 days" +%F)
D(){ date -d "+$1 days" +%F; }
# addline: $1=student $2=instructorProfile (vars I1P) $3=startDate (vars TODAY) → group line lessonCount 6
addline(){ bdy "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$CT\",\"instructorProfileId\":\"${2:-$I1P}\",\"startDate\":\"${3:-$TODAY}\",\"endDate\":\"$END\",\"lessonCount\":6,\"pricePerLesson\":100,\"makeupAllowance\":1,\"preferredMaxCapacity\":null,\"currency\":\"TRY\",\"preferredSlots\":[{\"dayOfWeek\":\"Monday\",\"preferredStartTime\":null}]}" "$ATOK")" | jget id; }
# mksession: $1=instr $2=date $3=time $4=capacity? $5=kind?
mksession(){ local x=""; [ -n "${4:-}" ] && x="$x,\"capacity\":$4"; [ -n "${5:-}" ] && x="$x,\"sessionKind\":\"$5\""; bdy "$(post /api/studio/class-sessions "{\"instructorProfileId\":\"$1\",\"classTypeId\":\"$CT\",\"startAt\":\"${2}T${3}:00\"$x}" "$ATOK")" | jget id; }
bookcode(){ pcode "/api/studio/class-sessions/$1/bookings" "{\"studentProfileId\":\"$2\"}" "$ATOK"; }
mvbody(){ post "/api/studio/class-sessions/$1/bookings/move" "$2" "${3:-$ATOK}"; }
mvcode(){ pcode "/api/studio/class-sessions/$1/bookings/move" "$2" "${3:-$ATOK}"; }
bkid(){ sql "SELECT \"Id\" FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$1' AND \"StudentProfileId\"='$2' AND \"Status\"='Booked' ORDER BY \"CreatedAt\" DESC LIMIT 1;"; }
bc(){ sql "SELECT \"BookedCount\" FROM \"ClassSessions\" WHERE \"Id\"='$1';"; }
bstat(){ sql "SELECT \"Status\" FROM \"ClassBookings\" WHERE \"Id\"='$1';"; }
breason(){ sql "SELECT \"CancellationReason\" FROM \"ClassBookings\" WHERE \"Id\"='$1';"; }
blineid(){ sql "SELECT \"StudentContractLineId\" FROM \"ClassBookings\" WHERE \"Id\"='$1';"; }
lineavail(){ gbody "/api/studio/students/$1/contract-lines" "$ATOK" | jq2 "d.data.find(l=>l.id==='$2')?.availableLessonCount"; }
SID=$(sql "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='S43 $TS' LIMIT 1;")
OUID=$(sql "SELECT \"OwnerUserId\" FROM \"Studios\" WHERE \"Id\"='$SID';")

echo "== Sprint 43 Move smoke (BASE=$BASE, studio=S43 $TS) =="
[ -n "$I1P" ] && [ -n "$CT" ] && ok "setup (owner/instr/classtype)" || bad "setup"

echo "--- T1) Tek öğrenciyi var olan derse taşı ---"
SA=$(guest SA); LA=$(addline "$SA"); SB=$(guest SB); LB=$(addline "$SB")
SRC1=$(mksession "$I1P" "$(D 3)" "12:00"); TGT1=$(mksession "$I1P" "$(D 3)" "14:00")
bookcode "$SRC1" "$SA" >/dev/null; bookcode "$SRC1" "$SB" >/dev/null
AV_BEFORE=$(lineavail "$SA" "$LA"); SA_BK=$(bkid "$SRC1" "$SA"); SB_BK=$(bkid "$SRC1" "$SB")
expectn "T1 setup: SRC BookedCount=2" 2 "$(bc "$SRC1")"
R1=$(mvbody "$SRC1" "{\"bookingIds\":[\"$SA_BK\"],\"targetSessionId\":\"$TGT1\"}")
expect "T1 move → 200" 200 "$(code "$R1")"
expectn "T1 movedCount=1" 1 "$(bdy "$R1" | jq2 'd.data.movedCount')"
expectn "T1 source BookedCount -1 → 1" 1 "$(bc "$SRC1")"
expectn "T1 target BookedCount +1 → 1" 1 "$(bc "$TGT1")"
expect "T1 source booking → CancelledByStudio" CancelledByStudio "$(bstat "$SA_BK")"
expect "T1 source booking reason = Taşındı" "Taşındı" "$(breason "$SA_BK")"
NB1=$(bkid "$TGT1" "$SA"); expect "T1 target booking Booked" Booked "$(bstat "$NB1")"
expect "T1 StudentContractLineId aynı taşındı" "$LA" "$(blineid "$NB1")"
expect "T1 kalan öğrenci (SB) etkilenmedi" Booked "$(bstat "$SB_BK")"
expectn "T1 ders hakkı nötr (available aynı)" "$AV_BEFORE" "$(lineavail "$SA" "$LA")"

echo "--- T2) Çoklu öğrenciyi var olan derse taşı (kalan etkilenmez + aggregate audit) ---"
SC=$(guest SC); addline "$SC" >/dev/null; SD=$(guest SD); addline "$SD" >/dev/null; SE=$(guest SE); addline "$SE" >/dev/null
SRC2=$(mksession "$I1P" "$(D 13)" "12:00"); TGT2=$(mksession "$I1P" "$(D 13)" "14:00")
bookcode "$SRC2" "$SC" >/dev/null; bookcode "$SRC2" "$SD" >/dev/null; bookcode "$SRC2" "$SE" >/dev/null
SC_BK=$(bkid "$SRC2" "$SC"); SD_BK=$(bkid "$SRC2" "$SD"); SE_BK=$(bkid "$SRC2" "$SE")
R2=$(mvbody "$SRC2" "{\"bookingIds\":[\"$SC_BK\",\"$SD_BK\"],\"targetSessionId\":\"$TGT2\"}")
expect "T2 move → 200" 200 "$(code "$R2")"
expectn "T2 movedCount=2" 2 "$(bdy "$R2" | jq2 'd.data.movedCount')"
expectn "T2 source BookedCount=1 (SE kaldı)" 1 "$(bc "$SRC2")"
expectn "T2 target BookedCount=2" 2 "$(bc "$TGT2")"
expect "T2 SC target booking Booked" Booked "$(bstat "$(bkid "$TGT2" "$SC")")"
expect "T2 SD target booking Booked" Booked "$(bstat "$(bkid "$TGT2" "$SD")")"
expect "T2 kalan SE etkilenmedi" Booked "$(bstat "$SE_BK")"
expectn "T2 aggregate audit: tek BookingMoved (target)" 1 "$(sql "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='BookingMoved' AND \"TargetId\"='$TGT2';")"

echo "--- T3) Yeni boşlukta ders açıp taşı (createTargetSession) ---"
SF=$(guest SF); addline "$SF" >/dev/null
SRC3=$(mksession "$I1P" "$(D 14)" "12:00"); bookcode "$SRC3" "$SF" >/dev/null; SF_BK=$(bkid "$SRC3" "$SF")
SESS_BEFORE=$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"StudioId\"='$SID';")
R3=$(mvbody "$SRC3" "{\"bookingIds\":[\"$SF_BK\"],\"createTargetSession\":{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$(D 14)T16:00:00\",\"capacity\":null}}")
expect "T3 move → 200" 200 "$(code "$R3")"
expect "T3 targetSessionCreated=true" true "$(bdy "$R3" | jq2 'd.data.targetSessionCreated')"
NEWSID=$(bdy "$R3" | jq2 'd.data.targetSessionId')
expectn "T3 yeni ClassSession oluştu (+1)" "$((SESS_BEFORE+1))" "$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"StudioId\"='$SID';")"
expectn "T3 yeni hedef BookedCount=1" 1 "$(bc "$NEWSID")"
expectn "T3 source BookedCount=0" 0 "$(bc "$SRC3")"
expect "T3 SF yeni derste Booked" Booked "$(bstat "$(bkid "$NEWSID" "$SF")")"

echo "--- T4) Rollback (var olan hedef, 2. öğrenci çakışma → tüm işlem geri alınır) ---"
SG=$(guest SG); addline "$SG" >/dev/null; SH=$(guest SH); addline "$SH" >/dev/null; addline "$SH" "$I2P" >/dev/null
SRC4=$(mksession "$I1P" "$(D 4)" "12:00"); TGT4=$(mksession "$I1P" "$(D 4)" "15:00"); OTHER4=$(mksession "$I2P" "$(D 4)" "15:00")
bookcode "$SRC4" "$SG" >/dev/null; bookcode "$SRC4" "$SH" >/dev/null; bookcode "$OTHER4" "$SH" >/dev/null
SG_BK=$(bkid "$SRC4" "$SG"); SH_BK=$(bkid "$SRC4" "$SH")
expectn "T4 setup: SRC4 BookedCount=2" 2 "$(bc "$SRC4")"
expect "T4 move (SG+SH, SH çakışır) → 409" 409 "$(mvcode "$SRC4" "{\"bookingIds\":[\"$SG_BK\",\"$SH_BK\"],\"targetSessionId\":\"$TGT4\"}")"
expectn "T4 rollback: SRC4 BookedCount=2 (değişmedi)" 2 "$(bc "$SRC4")"
expect "T4 rollback: SG source booking hâlâ Booked" Booked "$(bstat "$SG_BK")"
expectn "T4 rollback: TGT4 BookedCount=0 (booking oluşmadı)" 0 "$(bc "$TGT4")"

echo "--- T4b) Rollback (yeni hedef ders, 2. fail → yeni ders de geri alınır) ---"
SG2=$(guest SG2); addline "$SG2" >/dev/null; SH2=$(guest SH2); addline "$SH2" >/dev/null; addline "$SH2" "$I2P" >/dev/null
SRC4b=$(mksession "$I1P" "$(D 5)" "12:00"); OTHER4b=$(mksession "$I2P" "$(D 5)" "16:00")
bookcode "$SRC4b" "$SG2" >/dev/null; bookcode "$SRC4b" "$SH2" >/dev/null; bookcode "$OTHER4b" "$SH2" >/dev/null
SG2_BK=$(bkid "$SRC4b" "$SG2"); SH2_BK=$(bkid "$SRC4b" "$SH2")
expect "T4b move (createTargetSession, SH2 çakışır) → 409" 409 "$(mvcode "$SRC4b" "{\"bookingIds\":[\"$SG2_BK\",\"$SH2_BK\"],\"createTargetSession\":{\"instructorProfileId\":\"$I1P\",\"classTypeId\":\"$CT\",\"startAt\":\"$(D 5)T16:00:00\",\"capacity\":null}}")"
expectn "T4b rollback: yeni hedef ders KALMADI (I1P $(D 5) 16:00 = 0)" 0 "$(sql "SELECT count(*) FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"InstructorProfileId\"='$I1P' AND \"StartAt\"='$(D 5)T16:00:00';")"
expectn "T4b rollback: SRC4b BookedCount=2" 2 "$(bc "$SRC4b")"
expect "T4b rollback: SG2 hâlâ Booked" Booked "$(bstat "$SG2_BK")"

echo "--- T5) Hedef kapasite yetersiz → red ---"
SK=$(guest SK); addline "$SK" >/dev/null; SL=$(guest SL); addline "$SL" >/dev/null
SRC5=$(mksession "$I1P" "$(D 15)" "12:00"); TGT5=$(mksession "$I1P" "$(D 15)" "14:00" 1)
bookcode "$TGT5" "$SK" >/dev/null; bookcode "$SRC5" "$SL" >/dev/null; SL_BK=$(bkid "$SRC5" "$SL")
expect "T5 move (hedef dolu, cap=1) → 409" 409 "$(mvcode "$SRC5" "{\"bookingIds\":[\"$SL_BK\"],\"targetSessionId\":\"$TGT5\"}")"
expectn "T5 SRC5 BookedCount değişmedi (1)" 1 "$(bc "$SRC5")"
expectn "T5 TGT5 BookedCount değişmedi (1)" 1 "$(bc "$TGT5")"
expect "T5 SL hâlâ Booked" Booked "$(bstat "$SL_BK")"

echo "--- T6) Hedefte duplicate booking → red + rollback ---"
SM=$(guest SM); addline "$SM" >/dev/null
SRC6=$(mksession "$I1P" "$(D 6)" "12:00"); TGT6=$(mksession "$I1P" "$(D 6)" "14:00")
bookcode "$SRC6" "$SM" >/dev/null; bookcode "$TGT6" "$SM" >/dev/null
SM_SRC=$(bkid "$SRC6" "$SM")
expect "T6 move (SM hedefte zaten var) → 409" 409 "$(mvcode "$SRC6" "{\"bookingIds\":[\"$SM_SRC\"],\"targetSessionId\":\"$TGT6\"}")"
expect "T6 rollback: SM source hâlâ Booked" Booked "$(bstat "$SM_SRC")"
expectn "T6 SRC6 BookedCount=1" 1 "$(bc "$SRC6")"
expectn "T6 TGT6 BookedCount=1 (duplicate oluşmadı)" 1 "$(bc "$TGT6")"

echo "--- T7) Öğrenci hedef saatte başka derste → red ---"
SN=$(guest SN); addline "$SN" >/dev/null; addline "$SN" "$I2P" >/dev/null
SRC7=$(mksession "$I1P" "$(D 7)" "12:00"); OTHER7=$(mksession "$I2P" "$(D 7)" "15:00"); TGT7=$(mksession "$I1P" "$(D 7)" "15:00")
bookcode "$SRC7" "$SN" >/dev/null; bookcode "$OTHER7" "$SN" >/dev/null; SN_SRC=$(bkid "$SRC7" "$SN")
expect "T7 move (SN 15:00'te başka derste) → 409" 409 "$(mvcode "$SRC7" "{\"bookingIds\":[\"$SN_SRC\"],\"targetSessionId\":\"$TGT7\"}")"
expect "T7 rollback: SN source hâlâ Booked" Booked "$(bstat "$SN_SRC")"
expectn "T7 SRC7 BookedCount=1" 1 "$(bc "$SRC7")"

echo "--- T8) Yoklaması alınmış booking → red ---"
SO=$(guest SO); addline "$SO" >/dev/null
SRC8=$(mksession "$I1P" "$(D 8)" "12:00"); TGT8=$(mksession "$I1P" "$(D 8)" "14:00")
bookcode "$SRC8" "$SO" >/dev/null; SO_BK=$(bkid "$SRC8" "$SO")
sql "INSERT INTO \"Attendances\" (\"Id\",\"StudioId\",\"ClassBookingId\",\"ClassSessionId\",\"StudentProfileId\",\"Status\",\"MarkedByUserId\",\"MarkedAt\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$SID','$SO_BK','$SRC8','$SO','Present','$OUID',now(),now(),false);" >/dev/null
expect "T8 move (yoklama var) → 409" 409 "$(mvcode "$SRC8" "{\"bookingIds\":[\"$SO_BK\"],\"targetSessionId\":\"$TGT8\"}")"
expect "T8 SO booking status değişmedi (Booked)" Booked "$(bstat "$SO_BK")"
expectn "T8 SRC8 BookedCount=1" 1 "$(bc "$SRC8")"

echo "--- T9) Makeup/non-Regular kaynak → red (yalnız Regular) ---"
SP=$(guest SP)
SRC9=$(mksession "$I1P" "$(D 9)" "12:00" "" "Special"); TGT9=$(mksession "$I1P" "$(D 9)" "14:00")
bookcode "$SRC9" "$SP" >/dev/null; SP_BK=$(bkid "$SRC9" "$SP")
R9=$(mvbody "$SRC9" "{\"bookingIds\":[\"$SP_BK\"],\"targetSessionId\":\"$TGT9\"}")
expect "T9 move (Special kaynak) → 409" 409 "$(code "$R9")"
expect "T9 hata mesajı 'Regular' içerir" yes "$(has "$(bdy "$R9")" "Regular")"
expect "T9 SP hâlâ Booked" Booked "$(bstat "$SP_BK")"

echo "--- T10) Geçmiş kaynak → red ---"
SQ=$(guest SQ); addline "$SQ" >/dev/null
SRC10=$(mksession "$I1P" "$(D 10)" "12:00"); TGT10=$(mksession "$I1P" "$(D 10)" "14:00")
bookcode "$SRC10" "$SQ" >/dev/null; SQ_BK=$(bkid "$SRC10" "$SQ")
sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$(D -2)T12:00:00',\"EndAt\"='$(D -2)T13:00:00' WHERE \"Id\"='$SRC10';" >/dev/null
expect "T10 move (geçmiş kaynak) → 409" 409 "$(mvcode "$SRC10" "{\"bookingIds\":[\"$SQ_BK\"],\"targetSessionId\":\"$TGT10\"}")"
expect "T10 SQ hâlâ Booked" Booked "$(bstat "$SQ_BK")"

echo "--- T11) Completed / Cancelled kaynak → red ---"
SR=$(guest SR); addline "$SR" >/dev/null; SR2=$(guest SR2); addline "$SR2" >/dev/null
SRC11a=$(mksession "$I1P" "$(D 11)" "12:00"); SRC11b=$(mksession "$I1P" "$(D 11)" "13:00"); TGT11=$(mksession "$I1P" "$(D 11)" "15:00")
bookcode "$SRC11a" "$SR" >/dev/null; bookcode "$SRC11b" "$SR2" >/dev/null
SR_BK=$(bkid "$SRC11a" "$SR"); SR2_BK=$(bkid "$SRC11b" "$SR2")
sql "UPDATE \"ClassSessions\" SET \"Status\"='Completed' WHERE \"Id\"='$SRC11a';" >/dev/null
sql "UPDATE \"ClassSessions\" SET \"Status\"='Cancelled' WHERE \"Id\"='$SRC11b';" >/dev/null
expect "T11 move (Completed kaynak) → 409" 409 "$(mvcode "$SRC11a" "{\"bookingIds\":[\"$SR_BK\"],\"targetSessionId\":\"$TGT11\"}")"
expect "T11 move (Cancelled kaynak) → 409" 409 "$(mvcode "$SRC11b" "{\"bookingIds\":[\"$SR2_BK\"],\"targetSessionId\":\"$TGT11\"}")"

echo "--- T12) Hedef geçmiş: allowPastBooking false→red, true→ok ---"
SS=$(guest SS); addline "$SS" "$I1P" "$(D -10)" >/dev/null
SRC12=$(mksession "$I1P" "$(D 12)" "12:00"); TGT12=$(mksession "$I1P" "$(D 12)" "14:00")
bookcode "$SRC12" "$SS" >/dev/null; SS_BK=$(bkid "$SRC12" "$SS")
sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$(D -2)T14:00:00',\"EndAt\"='$(D -2)T15:00:00' WHERE \"Id\"='$TGT12';" >/dev/null
expect "T12a move (hedef geçmiş, allowPast=false) → 409" 409 "$(mvcode "$SRC12" "{\"bookingIds\":[\"$SS_BK\"],\"targetSessionId\":\"$TGT12\"}")"
expect "T12a SS hâlâ kaynakta Booked" Booked "$(bstat "$SS_BK")"
expect "T12b move (hedef geçmiş, allowPast=true, Owner) → 200" 200 "$(mvcode "$SRC12" "{\"bookingIds\":[\"$SS_BK\"],\"targetSessionId\":\"$TGT12\",\"allowPastBooking\":true}")"
expect "T12b SS hedef geçmiş derste Booked" Booked "$(bstat "$(bkid "$TGT12" "$SS")")"
# Sprint 42 regresyon: mevcut staff geçmiş-booking akışı PastBookingCreated audit'ini hâlâ üretir.
SX=$(guest SX); addline "$SX" "$I1P" "$(D -10)" >/dev/null
PASTSESS=$(mksession "$I1P" "$(D 16)" "12:00"); sql "UPDATE \"ClassSessions\" SET \"StartAt\"='$(D -2)T12:00:00',\"EndAt\"='$(D -2)T13:00:00' WHERE \"Id\"='$PASTSESS';" >/dev/null
pcode "/api/studio/class-sessions/$PASTSESS/bookings" "{\"studentProfileId\":\"$SX\",\"allowPastBooking\":true}" "$ATOK" >/dev/null
expectn "T12c Sprint 42 regresyon: PastBookingCreated audit üretildi" 1 "$(sql "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='PastBookingCreated' AND \"StudioId\"='$SID';")"

echo "--- T13) Instructor yetkisi → 403 ---"
expect "T13 Instructor move endpoint → 403" 403 "$(mvcode "$SRC1" "{\"bookingIds\":[\"$SA_BK\"],\"targetSessionId\":\"$TGT1\"}" "$I1TOK")"

echo "--- T14) Self-service yok (Student → 403; /api/student move yok) ---"
STOK=$(mkstudent_user stud43)
expect "T14 Student move endpoint → 403" 403 "$(mvcode "$SRC1" "{\"bookingIds\":[\"$SA_BK\"],\"targetSessionId\":\"$TGT1\"}" "$STOK")"
expect "T14 /api/student/.../move yok → 404" 404 "$(pcode "/api/student/class-sessions/$SRC1/bookings/move" "{}" "$STOK")"

echo "--- T15) Audit: BookingMoved oluşur, Detail PII içermez ---"
expectn "T15 T1 için BookingMoved audit var" 1 "$(sql "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='BookingMoved' AND \"TargetId\"='$TGT1';")"
DET=$(sql "SELECT \"Detail\" FROM \"SecurityAuditLogs\" WHERE \"EventType\"='BookingMoved' AND \"TargetId\"='$TGT1' ORDER BY \"OccurredAt\" DESC LIMIT 1;")
expect "T15 Detail 'source=' içerir" yes "$(has "$DET" 'source=')"
expect "T15 Detail 'count=' içerir" yes "$(has "$DET" 'count=')"
expect "T15 Detail PII yok (e-posta '@' yok)" no "$(has "$DET" '@')"
echo "     Detail örneği: $DET"

echo
echo "== Bırakılan test verisi: studio 'S43 $TS' (SID=$SID) — dev DB'de kalır. =="
[ "$FAILED" = "0" ] && { printf "\033[36mSPRINT 43 SMOKE: TÜM AKIŞLAR GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mSPRINT 43 SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
