#!/usr/bin/env bash
# Pliaforma — Evrim Mutlu Pilates Studyo DEMO seed.
#  - Stüdyo: "Evrim Mutlu Pilates Studyo" · Owner+Hoca: Evrim Mutlu · Çalışan hoca: Şeyda Kantarcı
#  - 20 öğrenci: 16 sözleşmeli (gruba + bireysele dağıtılmış, iki hocaya dağıtılmış, farklı kalan dersler)
#    + 4 yeni kayıt (misafir, sözleşmesiz). Bazılarında ÖNCEKİ (tamamlanmış) sözleşme de var.
#  - İşlenmiş/planlanmış dersler SQL ile (gerçekçi planned/delivered/remaining). Bazı ödemeler.
# Kullanım: scripts/evrim-demo-seed.sh [BASE]   (vars. http://localhost:5132)
set -u
BASE="${1:-http://localhost:5132}"
PW="Demo!2345x"
ok(){ printf "  \033[32m✓\033[0m %s\n" "$1"; }
jget(){ grep -o "\"$1\":\"[^\"]*\"" | head -1 | sed "s/\"$1\":\"\\([^\"]*\\)\"/\\1/"; }
jq2(){ node -e "try{const d=JSON.parse(require('fs').readFileSync(0,'utf8'));let r=($1);console.log(r==null?'':r)}catch(e){console.log('')}"; }
# Türkçe karakterler için body'yi dosyaya yazıp --data-binary ile gönder (inline -d UTF-8'i bozuyor).
post(){ local f; f=$(mktemp /c/tmp/pf-body-XXXXXX.json); printf '%s' "${2:-{\}}" > "$f"; curl -s -w "\n%{http_code}" -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} --data-binary @"$f"; rm -f "$f"; }
get(){ curl -s "$BASE$1" -H "Authorization: Bearer $2"; }
body(){ echo "$1" | sed '$d'; }
login(){ body "$(post /api/auth/login "{\"email\":\"$1\",\"password\":\"$PW\"}")" | jget accessToken; }
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres container yok."; exit 2; }
sqlv(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" >/dev/null 2>&1; }

OWNER=evrim@evrimmutlupilates.local
SEYDA=seyda@evrimmutlupilates.local

echo "== 1) Stüdyo + Owner (Evrim Mutlu) =="
OTOK=$(body "$(post /api/auth/register-studio "{\"studioName\":\"Evrim Mutlu Pilates Studyo\",\"ownerFullName\":\"Evrim Mutlu\",\"email\":\"$OWNER\",\"password\":\"$PW\"}")" | jget accessToken)
[ -z "$OTOK" ] && { echo "  HATA: stüdyo oluşturulamadı"; exit 1; }
SID=$(sqlv "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='Evrim Mutlu Pilates Studyo' LIMIT 1;")
OUID=$(sqlv "SELECT \"OwnerUserId\" FROM \"Studios\" WHERE \"Id\"='$SID';")
ok "Stüdyo: Evrim Mutlu Pilates Studyo  ·  Owner: $OWNER"

echo "== 2) Evrim'i HOCA yap (owner + instructor) + Şeyda Kantarcı (çalışan hoca) =="
# Evrim'in InstructorProfile'ı (owner userId ile) → owner aynı zamanda hoca; members LEFT JOIN ile dropdown'da görünür.
sql "INSERT INTO \"InstructorProfiles\" (\"Id\",\"StudioId\",\"UserId\",\"DisplayName\",\"Rate\",\"RateType\",\"IsAcceptingStudents\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$SID','$OUID','Evrim Mutlu',600,'PerSession',true,now(),false);"
EVR=$(sqlv "SELECT \"Id\" FROM \"InstructorProfiles\" WHERE \"StudioId\"='$SID' AND \"UserId\"='$OUID' LIMIT 1;")
# Şeyda: davet + kabul → profil normal akışla.
post /api/auth/register "{\"fullName\":\"Şeyda Kantarcı\",\"email\":\"$SEYDA\",\"password\":\"$PW\"}" >/dev/null
sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$SEYDA';"
M=$(body "$(post /api/studio/memberships/invite "{\"email\":\"$SEYDA\",\"role\":\"Instructor\",\"rate\":450}" "$OTOK")" | jget membershipId)
STOK=$(login "$SEYDA"); post "/api/members/$M/accept" '{}' "$STOK" >/dev/null
SEY=$(get /api/studio/memberships "$OTOK" | tr '{' '\n' | grep "\"email\":\"$SEYDA\"" | grep -o '"instructorProfileId":"[^"]*"' | head -1 | sed 's/.*:"\([^"]*\)"/\1/')
{ [ -z "$EVR" ] || [ -z "$SEY" ]; } && { echo "  HATA: hoca profili oluşmadı"; exit 1; }
ok "Hoca: Evrim Mutlu (owner+hoca)  ·  Hoca: Şeyda Kantarcı"

echo "== 3) Ders tipleri =="
ctype(){ body "$(post /api/class-types "{\"name\":\"$1\",\"defaultDurationMinutes\":$2}" "$OTOK")" | jget id; }
REF=$(ctype "Reformer Grup" 50)
MAT=$(ctype "Mat Grup" 50)
BIR=$(ctype "Bireysel Reformer" 60)
ok "Reformer Grup · Mat Grup · Bireysel Reformer"

TODAY=$(date +%F); END90=$(date -d "+90 days" +%F)
# Tercih slotları (29B aday seçimi için anlamlı)
G_MWF='[{"dayOfWeek":"Monday","preferredStartTime":"18:00:00"},{"dayOfWeek":"Wednesday","preferredStartTime":"18:00:00"},{"dayOfWeek":"Friday","preferredStartTime":"18:00:00"}]'
G_TT='[{"dayOfWeek":"Tuesday","preferredStartTime":"19:00:00"},{"dayOfWeek":"Thursday","preferredStartTime":"19:00:00"}]'
G_SS='[{"dayOfWeek":"Saturday","preferredStartTime":"11:00:00"}]'
I_MW='[{"dayOfWeek":"Monday","preferredStartTime":"20:00:00"},{"dayOfWeek":"Wednesday","preferredStartTime":"20:00:00"}]'
I_TT='[{"dayOfWeek":"Tuesday","preferredStartTime":"10:00:00"},{"dayOfWeek":"Thursday","preferredStartTime":"10:00:00"}]'

guest(){ body "$(post /api/studio/students/guest "{\"fullName\":\"$1\",\"source\":\"$2\"${3:+,\"level\":\"$3\"}}" "$OTOK")" | jget id; }
# mkline student instr ct individual lessons slots → lineId
mkline(){ body "$(post "/api/studio/students/$1/contract-lines" "{\"classTypeId\":\"$3\",\"instructorProfileId\":\"$2\",\"startDate\":\"$TODAY\",\"endDate\":\"$END90\",\"lessonCount\":$5,\"pricePerLesson\":$6,\"makeupAllowance\":2,\"isIndividual\":$4,\"currency\":\"TRY\",\"preferredSlots\":$7}" "$OTOK")" | jget id; }
hdrof(){ get "/api/studio/students/$1/contract-lines" "$OTOK" | jq2 "d.data.find(l=>l.id==='$2')?.studentContractId"; }
# bookcnt line hdr student instr ct cap indiv count status startExpr — SQL: count seans+booking ekle
bookcnt(){ local line="$1" hdr="$2" stu="$3" instr="$4" ct="$5" cap="$6" indiv="$7" cnt="$8" status="$9" base="${10}"
  [ "$cnt" -le 0 ] && return 0
  sql "WITH s AS (INSERT INTO \"ClassSessions\" (\"Id\",\"StudioId\",\"InstructorProfileId\",\"ClassTypeId\",\"IsIndividual\",\"StartAt\",\"EndAt\",\"Capacity\",\"BookedCount\",\"Status\",\"SessionKind\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\")
    SELECT gen_random_uuid(),'$SID','$instr','$ct',$indiv, ($base + (g * interval '1 day')), ($base + (g * interval '1 day') + interval '50 minutes'), $cap, 1, '$status','Regular','$OUID', now(), false
    FROM generate_series(1,$cnt) g RETURNING \"Id\")
    INSERT INTO \"ClassBookings\" (\"Id\",\"StudioId\",\"ClassSessionId\",\"StudentProfileId\",\"StudentContractId\",\"StudentContractLineId\",\"Status\",\"BookedByUserId\",\"RequiresPayment\",\"CreatedAt\",\"IsDeleted\")
    SELECT gen_random_uuid(),'$SID', s.\"Id\",'$stu','$hdr','$line','Booked','$OUID',false, now(), false FROM s;"
}
deliver(){ bookcnt "$1" "$2" "$3" "$4" "$5" "$6" "$7" "$8" Completed "timestamp '2024-10-01 10:00:00'"; }
planf(){ bookcnt "$1" "$2" "$3" "$4" "$5" "$6" "$7" "$8" Scheduled "(now() + interval '2 days')"; }
payline(){ local pid; pid=$(body "$(post "/api/studio/students/$1/payments" "{\"amount\":$3,\"dueDate\":\"$4\",\"studentContractLineId\":\"$2\"}" "$OTOK")" | jget id); [ "${5:-}" = paid ] && post "/api/studio/payments/$pid/mark-paid" '{}' "$OTOK" >/dev/null; }

# enroll: name instr ct indiv cap lessons delivered planned perLesson slots level → "sid|line|hdr"
enroll(){ local sid line hdr
  sid=$(guest "$1" Phone "${11:-}")
  line=$(mkline "$sid" "$2" "$3" "$4" "$6" "$9" "${10}")
  hdr=$(hdrof "$sid" "$line")
  deliver "$line" "$hdr" "$sid" "$2" "$3" "$5" "$4" "$7"
  planf "$line" "$hdr" "$sid" "$2" "$3" "$5" "$4" "$8"
  echo "$sid|$line|$hdr"
}
# prevcontract: sid instr ct indiv cap lessons perLesson slots — geçmiş tamamlanmış sözleşme (delivered=lessons, Completed, geçmiş tarih)
prevcontract(){ local sid="$1" line hdr
  line=$(mkline "$sid" "$2" "$3" "$4" "$6" "$7" "$8"); hdr=$(hdrof "$sid" "$line")
  deliver "$line" "$hdr" "$sid" "$2" "$3" "$5" "$4" "$6"
  sql "UPDATE \"StudentContractLines\" SET \"Status\"='Completed', \"StartDate\"='2024-09-01', \"EndDate\"='2024-12-15' WHERE \"Id\"='$line';"
}

echo "== 4) Öğrenciler (16 sözleşmeli — iki hocaya + grup/bireysele dağıtılmış, farklı kalan dersler) =="
# Grup — Evrim (Reformer Grup, MWF 18:00)
R1=$(enroll "Ayşe Demir"     "$EVR" "$REF" false 9999 12 5 3 350 "$G_MWF" Intermediate)   # delivered5 planned3 → kalan7 planlanabilir4
R2=$(enroll "Burak Şahin"    "$EVR" "$REF" false 9999 12 2 4 350 "$G_MWF" Beginner)
R3=$(enroll "Ceyda Korkmaz"  "$EVR" "$REF" false 9999 16 9 2 350 "$G_MWF" Advanced)
R4=$(enroll "Deniz Aksoy"    "$EVR" "$REF" false 9999 10 0 5 350 "$G_MWF" Beginner)        # hiç işlenmemiş
# Grup — Şeyda (Mat Grup, TT 19:00)
R5=$(enroll "Elif Yıldız"    "$SEY" "$MAT" false 9999 8  4 2 300 "$G_TT" Intermediate)
R6=$(enroll "Furkan Çelik"   "$SEY" "$MAT" false 9999 8  7 1 300 "$G_TT" Beginner)         # neredeyse bitmiş
R7=$(enroll "Gizem Arslan"   "$SEY" "$MAT" false 9999 12 6 3 300 "$G_TT" Advanced)
R8=$(enroll "Hakan Öztürk"   "$SEY" "$MAT" false 9999 10 3 2 300 "$G_TT" Beginner)
# Grup — Şeyda (Reformer Grup, Cmt 11:00)
R9=$(enroll "Irmak Şen"      "$SEY" "$REF" false 9999 12 8 1 350 "$G_SS" Intermediate)
R10=$(enroll "Kaan Polat"    "$SEY" "$REF" false 9999 16 11 2 350 "$G_SS" Advanced)
# Bireysel — Evrim (Bireysel Reformer, MW 20:00)
R11=$(enroll "Leyla Acar"    "$EVR" "$BIR" true 1 10 4 2 700 "$I_MW" Intermediate)
R12=$(enroll "Mert Doğan"    "$EVR" "$BIR" true 1 8  1 3 700 "$I_MW" Beginner)
# Bireysel — Şeyda (Bireysel Reformer, TT 10:00)
R13=$(enroll "Nehir Kaya"    "$SEY" "$BIR" true 1 12 7 2 650 "$I_TT" Advanced)
R14=$(enroll "Onur Yılmaz"   "$SEY" "$BIR" true 1 10 5 1 650 "$I_TT" Intermediate)
R15=$(enroll "Pınar Güneş"   "$EVR" "$BIR" true 1 8  2 2 700 "$I_TT" Beginner)
R16=$(enroll "Rüya Tekin"    "$SEY" "$MAT" false 9999 10 6 1 300 "$G_TT" Advanced)
ok "16 aktif sözleşmeli öğrenci (Evrim: 6 · Şeyda: 10 · grup+bireysel · farklı kalan dersler)"

echo "== 5) Önceki (tamamlanmış) sözleşmeler — bazı öğrencilerde geçmiş paket =="
prevcontract "$(echo "$R1"  | cut -d'|' -f1)" "$EVR" "$REF" false 9999 12 350 "$G_MWF"
prevcontract "$(echo "$R3"  | cut -d'|' -f1)" "$EVR" "$REF" false 9999 8  350 "$G_MWF"
prevcontract "$(echo "$R5"  | cut -d'|' -f1)" "$SEY" "$MAT" false 9999 10 300 "$G_TT"
prevcontract "$(echo "$R9"  | cut -d'|' -f1)" "$SEY" "$REF" false 9999 12 350 "$G_SS"
prevcontract "$(echo "$R11" | cut -d'|' -f1)" "$EVR" "$BIR" true 1 8 700 "$I_MW"
prevcontract "$(echo "$R13" | cut -d'|' -f1)" "$SEY" "$BIR" true 1 10 650 "$I_TT"
prevcontract "$(echo "$R7"  | cut -d'|' -f1)" "$SEY" "$MAT" false 9999 8 300 "$G_TT"
prevcontract "$(echo "$R14" | cut -d'|' -f1)" "$SEY" "$BIR" true 1 12 650 "$I_TT"
ok "8 öğrenciye önceki tamamlanmış sözleşme eklendi"

echo "== 6) Ödemeler (kimi tam, kimi kısmi/vadeli) =="
DUE=$(date -d "+20 days" +%F)
payline "$(echo "$R1" | cut -d'|' -f1)"  "$(echo "$R1" | cut -d'|' -f2)" 4200 "$TODAY" paid   # Ayşe tam
payline "$(echo "$R3" | cut -d'|' -f1)"  "$(echo "$R3" | cut -d'|' -f2)" 2800 "$TODAY" paid   # Ceyda kısmi ödendi
payline "$(echo "$R3" | cut -d'|' -f1)"  "$(echo "$R3" | cut -d'|' -f2)" 2800 "$DUE"          # Ceyda kalan vadeli
payline "$(echo "$R5" | cut -d'|' -f1)"  "$(echo "$R5" | cut -d'|' -f2)" 2400 "$TODAY" paid
payline "$(echo "$R7" | cut -d'|' -f1)"  "$(echo "$R7" | cut -d'|' -f2)" 1800 "$DUE"          # Gizem borçlu
payline "$(echo "$R11" | cut -d'|' -f1)" "$(echo "$R11" | cut -d'|' -f2)" 7000 "$TODAY" paid
payline "$(echo "$R13" | cut -d'|' -f1)" "$(echo "$R13" | cut -d'|' -f2)" 3900 "$DUE"          # Nehir borçlu
ok "Ödemeler: 4 tam · Ceyda kısmi+vadeli · Gizem/Nehir borçlu"

echo "== 7) 4 yeni kayıt (misafir, sözleşmesiz) =="
N1=$(guest "Sude Aydın" Web Beginner)
N2=$(guest "Tunç Erden" Referral)
N3=$(guest "Umut Kara" WalkIn Beginner)
N4=$(guest "Vera Şimşek" Web)
ok "Yeni kayıtlar: Sude Aydın · Tunç Erden · Umut Kara · Vera Şimşek (sözleşme yok)"

echo "== 8) 2 öğrenciye self-servis (öğrenci paneli) girişi =="
conv(){ post "/api/studio/students/$1/convert-to-user" "{\"email\":\"$2\",\"initialPassword\":\"$PW\"}" "$OTOK" >/dev/null; sql "UPDATE \"Users\" SET \"EmailVerifiedAt\"=now() WHERE \"Email\"='$2';"; }
conv "$(echo "$R1" | cut -d'|' -f1)" "ayse@demo.local"
conv "$(echo "$R9" | cut -d'|' -f1)" "irmak@demo.local"
ok "Öğrenci girişi: ayse@demo.local · irmak@demo.local"

echo
printf "\033[36mEVRIM DEMO SEED TAMAM ✅\033[0m  (tüm demo parolaları: %s)\n" "$PW"
STUDENTS=$(sqlv "SELECT count(*) FROM \"StudentProfiles\" WHERE \"StudioId\"='$SID';")
LINES=$(sqlv "SELECT count(*) FROM \"StudentContractLines\" WHERE \"StudioId\"='$SID';")
ACTIVE=$(sqlv "SELECT count(*) FROM \"StudentContractLines\" WHERE \"StudioId\"='$SID' AND \"Status\"='Active';")
COMPLETED=$(sqlv "SELECT count(*) FROM \"StudentContractLines\" WHERE \"StudioId\"='$SID' AND \"Status\"='Completed';")
echo "Öğrenci: $STUDENTS · Sözleşme satırı: $LINES (aktif $ACTIVE, önceki/tamamlanmış $COMPLETED)"
echo "Owner+Hoca: $OWNER · Hoca: $SEYDA · Öğrenci girişi: ayse@demo.local, irmak@demo.local"
