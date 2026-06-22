#!/usr/bin/env bash
# Evrim demo — ek: paylaşımlı GRUP dersleri (programda öğrenciler görünsün).
#  - Bu hafta (yaklaşan) Scheduled grup dersleri + çok öğrenci.
#  - Son birkaç gün (geçmiş) Completed + yoklama (Present) → "işlenmiş" geçmiş veri.
set -u
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
[ -z "$PGC" ] && { echo "Postgres yok."; exit 2; }
sqlv(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" 2>/dev/null; }
sql(){ docker exec "$PGC" psql -U postgres -d pliaforma -tA -c "$1" >/dev/null 2>&1; }

SID=$(sqlv "SELECT \"Id\" FROM \"Studios\" WHERE \"Name\"='Evrim Mutlu Pilates Studyo' LIMIT 1;")
[ -z "$SID" ] && { echo "Stüdyo yok (önce evrim-demo-seed.sh)."; exit 1; }
OUID=$(sqlv "SELECT \"OwnerUserId\" FROM \"Studios\" WHERE \"Id\"='$SID';")
EVR=$(sqlv "SELECT \"Id\" FROM \"InstructorProfiles\" WHERE \"StudioId\"='$SID' AND \"UserId\"='$OUID' LIMIT 1;")
SEY=$(sqlv "SELECT p.\"Id\" FROM \"InstructorProfiles\" p JOIN \"Users\" u ON u.\"Id\"=p.\"UserId\" WHERE p.\"StudioId\"='$SID' AND u.\"Email\" LIKE 'seyda@%' LIMIT 1;")
REF=$(sqlv "SELECT \"Id\" FROM \"ClassTypes\" WHERE \"StudioId\"='$SID' AND \"Name\"='Reformer Grup' LIMIT 1;")
MAT=$(sqlv "SELECT \"Id\" FROM \"ClassTypes\" WHERE \"StudioId\"='$SID' AND \"Name\"='Mat Grup' LIMIT 1;")

mksess(){ # instr ct 'YYYY-MM-DD HH:MM:00' status durmin → id
  sql "INSERT INTO \"ClassSessions\" (\"Id\",\"StudioId\",\"InstructorProfileId\",\"ClassTypeId\",\"IsIndividual\",\"StartAt\",\"EndAt\",\"Capacity\",\"BookedCount\",\"Status\",\"SessionKind\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\")
    VALUES (gen_random_uuid(),'$SID','$1','$2',false,'$3', ('$3'::timestamp + interval '$5 minutes'), 9999, 0, '$4','Regular','$OUID',now(),false);"
  sqlv "SELECT \"Id\" FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"InstructorProfileId\"='$1' AND \"StartAt\"='$3' ORDER BY \"CreatedAt\" DESC LIMIT 1;"
}
bookmatch(){ # sessionId ct instr limit — o ders tipi+hocanın aktif line'lı öğrencilerini ekle
  sql "INSERT INTO \"ClassBookings\" (\"Id\",\"StudioId\",\"ClassSessionId\",\"StudentProfileId\",\"StudentContractId\",\"StudentContractLineId\",\"Status\",\"BookedByUserId\",\"RequiresPayment\",\"CreatedAt\",\"IsDeleted\")
    SELECT gen_random_uuid(),'$SID','$1',c.\"StudentProfileId\",l.\"StudentContractId\",l.\"Id\",'Booked','$OUID',false,now(),false
    FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\"
    WHERE l.\"StudioId\"='$SID' AND l.\"Status\"='Active' AND l.\"ClassTypeId\"='$2' AND l.\"InstructorProfileId\"='$3'
    ORDER BY c.\"StudentProfileId\" LIMIT $4;"
  sql "UPDATE \"ClassSessions\" SET \"BookedCount\"=(SELECT count(*) FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$1' AND \"Status\"='Booked') WHERE \"Id\"='$1';"
}
bookany(){ # sessionId limit — KALABALIK ders için herhangi N aktif öğrenciyi (aktif line'ıyla) ekle
  sql "INSERT INTO \"ClassBookings\" (\"Id\",\"StudioId\",\"ClassSessionId\",\"StudentProfileId\",\"StudentContractId\",\"StudentContractLineId\",\"Status\",\"BookedByUserId\",\"RequiresPayment\",\"CreatedAt\",\"IsDeleted\")
    SELECT gen_random_uuid(),'$SID','$1', x.\"StudentProfileId\", x.\"StudentContractId\", x.\"LineId\",'Booked','$OUID',false,now(),false
    FROM (
      SELECT DISTINCT ON (c.\"StudentProfileId\") c.\"StudentProfileId\", l.\"StudentContractId\", l.\"Id\" AS \"LineId\"
      FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\"
      WHERE l.\"StudioId\"='$SID' AND l.\"Status\"='Active'
      ORDER BY c.\"StudentProfileId\"
    ) x LIMIT $2;"
  sql "UPDATE \"ClassSessions\" SET \"BookedCount\"=(SELECT count(*) FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$1' AND \"Status\"='Booked') WHERE \"Id\"='$1';"
}
attendAll(){ # sessionId — tüm Booked'lara Present yoklama (StudioId NOT NULL — atlanırsa insert sessizce düşer)
  sql "INSERT INTO \"Attendances\" (\"Id\",\"StudioId\",\"ClassBookingId\",\"ClassSessionId\",\"StudentProfileId\",\"Status\",\"MarkedByUserId\",\"MarkedAt\",\"CreatedAt\",\"IsDeleted\")
    SELECT gen_random_uuid(), b.\"StudioId\", b.\"Id\", b.\"ClassSessionId\", b.\"StudentProfileId\",'Present','$OUID',now(),now(),false
    FROM \"ClassBookings\" b WHERE b.\"ClassSessionId\"='$1' AND b.\"Status\"='Booked';"
}

D1=$(date -d "+1 day" +%F); D2=$(date -d "+2 days" +%F); D3=$(date -d "+3 days" +%F)
DM1=$(date -d "-1 day" +%F); DM2=$(date -d "-2 days" +%F); DM3=$(date -d "-3 days" +%F)

echo "== Yaklaşan paylaşımlı grup dersleri (öğrencili) =="
S1=$(mksess "$EVR" "$REF" "$D1 18:00:00" Scheduled 50); bookmatch "$S1" "$REF" "$EVR" 6
S2=$(mksess "$SEY" "$MAT" "$D2 19:00:00" Scheduled 50); bookmatch "$S2" "$MAT" "$SEY" 6
S3=$(mksess "$EVR" "$REF" "$D3 18:00:00" Scheduled 50); bookmatch "$S3" "$REF" "$EVR" 6
echo "  ✓ 3 yaklaşan grup dersi (Evrim Reformer · Şeyda Mat)"

echo "== Geçmiş (işlenmiş) grup dersleri — Completed + Present yoklama =="
P1=$(mksess "$EVR" "$REF" "$DM1 18:00:00" Completed 50); bookmatch "$P1" "$REF" "$EVR" 6; attendAll "$P1"
P2=$(mksess "$SEY" "$MAT" "$DM2 19:00:00" Completed 50); bookmatch "$P2" "$MAT" "$SEY" 6; attendAll "$P2"
P3=$(mksess "$EVR" "$REF" "$DM3 18:00:00" Completed 50); bookmatch "$P3" "$REF" "$EVR" 6; attendAll "$P3"
echo "  ✓ 3 geçmiş işlenmiş grup dersi (yoklama alınmış)"

echo "== KALABALIK grup dersleri (8-10 kişi) =="
B1=$(mksess "$EVR" "$REF" "$D1 11:00:00" Scheduled 60); bookany "$B1" 10
B2=$(mksess "$SEY" "$MAT" "$D2 11:00:00" Scheduled 60); bookany "$B2" 8
BB1=$(sqlv "SELECT \"BookedCount\" FROM \"ClassSessions\" WHERE \"Id\"='$B1';")
BB2=$(sqlv "SELECT \"BookedCount\" FROM \"ClassSessions\" WHERE \"Id\"='$B2';")
echo "  ✓ Evrim Reformer ($D1 11:00): $BB1 kişi · Şeyda Mat ($D2 11:00): $BB2 kişi"

CNT=$(sqlv "SELECT count(*) FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\" WHERE s.\"StudioId\"='$SID' AND s.\"Id\" IN ('$S1','$S2','$S3','$P1','$P2','$P3','$B1','$B2');")
echo "Toplam eklenen rezervasyon (8 derste): $CNT"
echo "Tarih aralığı: yaklaşan $D1..$D3 · geçmiş $DM3..$DM1 (programda tarih filtresini geriye alınca görünür)"

# Hoca self-çakışmasını gider: seed per-öğrenci seansları aynı hocaya örtüşen saatler üretebiliyor; bir hoca
# aynı anda tek ders yapabilir kuralına uygun olsun diye her (hoca,gün) seansları 09:00'dan 1'er saat yay.
echo "== Deconflict: hoca başına günlük seanslar saatlere yayılıyor (çakışma=0) =="
docker exec "$PGC" psql -U postgres -d pliaforma -c "
WITH ordered AS (
  SELECT \"Id\", row_number() OVER (PARTITION BY \"InstructorProfileId\", (\"StartAt\"::date) ORDER BY \"StartAt\",\"Id\") - 1 AS seq,
    (\"StartAt\"::date) AS d, EXTRACT(EPOCH FROM (\"EndAt\"-\"StartAt\"))/60 AS dur
  FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"Status\" IN ('Scheduled','Completed')
)
UPDATE \"ClassSessions\" s SET \"StartAt\" = (o.d + time '09:00') + (o.seq * interval '1 hour'),
    \"EndAt\" = (o.d + time '09:00') + (o.seq * interval '1 hour') + (o.dur * interval '1 minute')
FROM ordered o WHERE s.\"Id\"=o.\"Id\";" >/dev/null 2>&1
CONF=$(sqlv "SELECT count(*) FROM \"ClassSessions\" a JOIN \"ClassSessions\" b ON a.\"InstructorProfileId\"=b.\"InstructorProfileId\" AND a.\"Id\"<b.\"Id\" AND a.\"StartAt\" < b.\"EndAt\" AND b.\"StartAt\" < a.\"EndAt\" WHERE a.\"StudioId\"='$SID' AND a.\"Status\" IN ('Scheduled','Completed') AND b.\"Status\" IN ('Scheduled','Completed');")
echo "  ✓ hoca self-çakışma: $CONF"

# Yoklama backfill: TÜM Completed seanslardaki Booked'lara Present (Hoca İş Hacmi 'öğrenci/katılım' sütunu dolsun).
echo "== Yoklama backfill (Completed seanslar → Present) =="
docker exec "$PGC" psql -U postgres -d pliaforma -c "
INSERT INTO \"Attendances\" (\"Id\",\"StudioId\",\"ClassBookingId\",\"ClassSessionId\",\"StudentProfileId\",\"Status\",\"MarkedByUserId\",\"MarkedAt\",\"CreatedAt\",\"IsDeleted\")
SELECT gen_random_uuid(), b.\"StudioId\", b.\"Id\", b.\"ClassSessionId\", b.\"StudentProfileId\", 'Present', '$OUID', now(), now(), false
FROM \"ClassBookings\" b JOIN \"ClassSessions\" s ON s.\"Id\"=b.\"ClassSessionId\"
WHERE s.\"StudioId\"='$SID' AND s.\"Status\"='Completed' AND b.\"Status\"='Booked'
AND NOT EXISTS (SELECT 1 FROM \"Attendances\" a WHERE a.\"ClassBookingId\"=b.\"Id\");" >/dev/null 2>&1
echo "  ✓ Present yoklama: $(sqlv "SELECT count(*) FROM \"Attendances\" WHERE \"StudioId\"='$SID' AND \"Status\"='Present';")"

# Bugüne dersler (denemeler için) — gün boyu (08:00–17:00) yayılmış, çakışmasız, öğrencili.
echo "== Bugüne dersler (denemeler için) =="
TD=$(date +%F)
sql "DELETE FROM \"ClassBookings\" WHERE \"ClassSessionId\" IN (SELECT \"Id\" FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"StartAt\"::date='$TD');"
sql "DELETE FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"StartAt\"::date='$TD';"
mktoday(){ # instr ct HH limit — bugün $3:00–$3:50, ders tipine uyan öğrencilerle
  sql "INSERT INTO \"ClassSessions\" (\"Id\",\"StudioId\",\"InstructorProfileId\",\"ClassTypeId\",\"IsIndividual\",\"StartAt\",\"EndAt\",\"Capacity\",\"BookedCount\",\"Status\",\"SessionKind\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\")
    VALUES (gen_random_uuid(),'$SID','$1','$2',false,'$TD $3:00:00','$TD $3:50:00',9999,0,'Scheduled','Regular','$OUID',now(),false);"
  local s=$(sqlv "SELECT \"Id\" FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"InstructorProfileId\"='$1' AND \"StartAt\"='$TD $3:00:00' LIMIT 1;")
  sql "INSERT INTO \"ClassBookings\" (\"Id\",\"StudioId\",\"ClassSessionId\",\"StudentProfileId\",\"StudentContractId\",\"StudentContractLineId\",\"Status\",\"BookedByUserId\",\"RequiresPayment\",\"CreatedAt\",\"IsDeleted\")
    SELECT gen_random_uuid(),'$SID','$s',x.\"StudentProfileId\",x.\"StudentContractId\",x.\"LineId\",'Booked','$OUID',false,now(),false
    FROM (SELECT DISTINCT ON (c.\"StudentProfileId\") c.\"StudentProfileId\",l.\"StudentContractId\",l.\"Id\" AS \"LineId\" FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE l.\"StudioId\"='$SID' AND l.\"Status\"='Active' AND l.\"ClassTypeId\"='$2' ORDER BY c.\"StudentProfileId\") x LIMIT $4;"
  sql "UPDATE \"ClassSessions\" SET \"BookedCount\"=(SELECT count(*) FROM \"ClassBookings\" WHERE \"ClassSessionId\"='$s' AND \"Status\"='Booked') WHERE \"Id\"='$s';"
}
for HH in 08 10 12 14 16; do mktoday "$EVR" "$REF" "$HH" 5; done
for HH in 09 11 13 15 17; do mktoday "$SEY" "$MAT" "$HH" 5; done
echo "  ✓ bugün ($TD) ders sayısı: $(sqlv "SELECT count(*) FROM \"ClassSessions\" WHERE \"StudioId\"='$SID' AND \"StartAt\"::date='$TD' AND \"Status\"='Scheduled';")"

# Ödeme/borç çeşitliliği: 3 taksit (ödenmiş+geciken+yaklaşan) + önceki sözleşme borcu → Ödemeler sayfası özetleri dolsun.
echo "== Ödeme/borç çeşitliliği (taksit + geciken + önceki sözleşme) =="
docker exec "$PGC" psql -U postgres -d pliaforma -c "
WITH lines AS (SELECT l.\"Id\" lid,l.\"StudentContractId\" cid,c.\"StudentProfileId\" spid,l.\"TotalPrice\" tp,l.\"Currency\" cur, row_number() OVER (ORDER BY c.\"StudentProfileId\") rn FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE l.\"StudioId\"='$SID' AND l.\"Status\"='Active'), pick AS (SELECT * FROM lines WHERE rn<=8), inst(k,st,dd,paid) AS (VALUES (1,'Paid',('$TD'::date-60),true),(2,'Pending',('$TD'::date-10),false),(3,'Pending',('$TD'::date+25),false))
INSERT INTO \"Payments\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"StudentContractId\",\"StudentContractLineId\",\"Amount\",\"Currency\",\"DueDate\",\"PaidAt\",\"Status\",\"Notes\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\")
SELECT gen_random_uuid(),'$SID',p.spid,p.cid,p.lid,round(p.tp/3.0),p.cur,i.dd,CASE WHEN i.paid THEN now() ELSE NULL END,i.st,('Taksit '||i.k||'/3'),'$OUID',now(),false FROM pick p CROSS JOIN inst i;" >/dev/null 2>&1
docker exec "$PGC" psql -U postgres -d pliaforma -c "
WITH plines AS (SELECT l.\"Id\" lid,l.\"StudentContractId\" cid,c.\"StudentProfileId\" spid,l.\"Currency\" cur, row_number() OVER (ORDER BY c.\"StudentProfileId\") rn FROM \"StudentContractLines\" l JOIN \"StudentContracts\" c ON c.\"Id\"=l.\"StudentContractId\" WHERE l.\"StudioId\"='$SID' AND l.\"Status\"='Completed')
INSERT INTO \"Payments\" (\"Id\",\"StudioId\",\"StudentProfileId\",\"StudentContractId\",\"StudentContractLineId\",\"Amount\",\"Currency\",\"DueDate\",\"PaidAt\",\"Status\",\"Notes\",\"CreatedByUserId\",\"CreatedAt\",\"IsDeleted\")
SELECT gen_random_uuid(),'$SID',spid,cid,lid,1500,cur,('$TD'::date-45),NULL,'Pending','Önceki sözleşme bakiyesi','$OUID',now(),false FROM plines WHERE rn<=4;" >/dev/null 2>&1
echo "  ✓ borçlu öğrenci: $(sqlv "SELECT count(DISTINCT \"StudentProfileId\") FROM \"Payments\" WHERE \"StudioId\"='$SID' AND \"Status\"='Pending' AND \"PaidAt\" IS NULL;")"
