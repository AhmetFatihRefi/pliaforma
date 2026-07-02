#!/usr/bin/env bash
# Pliaforma — Sprint 47 legacy installment dry-run check (SALT-OKUNUR).
#   Sprint 45 spec'i (karar #4) "2+ aktif installment'lı line'larda line.DueDate = MAX(installment.DueDate)"
#   backfill'ini vaat etmişti; bu script backfill'in GEREKLİ OLUP OLMADIĞINI raporlar — kendisi hiçbir
#   UPDATE/DELETE ÇALIŞTIRMAZ. Sonuç "mismatch_count > 0" ise: backfill için AYRI onay + AYRI script gerekir.
# Kullanım: scripts/sprint47-legacy-installment-check.sh   (yalnız docker postgres container'ı okur)
set -u
PGC=$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)
if [ -z "$PGC" ]; then
  echo "Postgres container bulunamadı — kontrol atlandı (yalnız yerel/dev/staging'de anlamlı; pilot/prod'da operatör PG_CONTAINER ile çalıştırır)."
  exit 0
fi
PGUSER="${PGUSER:-postgres}"; PGDB="${PGDB:-pliaforma}"
sql(){ docker exec "$PGC" psql -U "$PGUSER" -d "$PGDB" -tA -c "$1" 2>/dev/null | tr -d '\r'; }

echo "== Sprint 47: legacy installment dry-run check (container=$PGC db=$PGDB) =="

ACTIVE=$(sql 'SELECT count(*) FROM "ContractLinePaymentInstallments" WHERE "IsActive"=true AND "IsDeleted"=false;')
echo "-- Aktif (IsActive=true, silinmemiş) installment satırı: ${ACTIVE:-0}"

MULTI=$(sql 'SELECT count(*) FROM (SELECT "StudentContractLineId" FROM "ContractLinePaymentInstallments" WHERE "IsActive"=true AND "IsDeleted"=false GROUP BY "StudentContractLineId" HAVING count(*) >= 2) x;')
echo "-- 2+ aktif installment'lı (gerçek taksitli) line sayısı: ${MULTI:-0}"

MISMATCH=$(sql '
  SELECT count(*) FROM (
    SELECT i."StudentContractLineId", max(i."DueDate") mx, l."DueDate" ld
    FROM "ContractLinePaymentInstallments" i JOIN "StudentContractLines" l ON l."Id" = i."StudentContractLineId"
    WHERE i."IsActive" = true AND i."IsDeleted" = false
    GROUP BY i."StudentContractLineId", l."DueDate"
  ) x WHERE mx IS DISTINCT FROM ld);')
echo "-- line.DueDate <> MAX(installment.DueDate) uyumsuzluğu: ${MISMATCH:-0}"

echo
if [ "${MISMATCH:-0}" = "0" ]; then
  echo "SONUC: Uyumsuzluk YOK — Sprint 45 legacy backfill'ine bu DB için gerek yok (dry-run temiz)."
else
  echo "SONUC: ${MISMATCH} line'da line.DueDate MAX(installment.DueDate) ile uyuşmuyor."
  echo "       Bu satırlar Sprint 45 borç motorunda (ContractLineFeature.ContractLineQuery.BuildAsync) YANLIŞ"
  echo "       gecikme/vade gösterebilir. UPDATE bu script ile YAPILMAZ — ayrı onay + ayrı backfill script gerekir."
  echo "       Etkilenen satırlar:"
  sql '
    SELECT i."StudentContractLineId" || E'"'"'\t'"'"' || max(i."DueDate")::text || E'"'"'\t'"'"' || l."DueDate"::text || E'"'"'\t'"'"' || l."Status"::text
    FROM "ContractLinePaymentInstallments" i JOIN "StudentContractLines" l ON l."Id" = i."StudentContractLineId"
    WHERE i."IsActive" = true AND i."IsDeleted" = false
    GROUP BY i."StudentContractLineId", l."DueDate", l."Status"
    HAVING max(i."DueDate") IS DISTINCT FROM l."DueDate";'
fi
