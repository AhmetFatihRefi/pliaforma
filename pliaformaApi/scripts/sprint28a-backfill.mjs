// Sprint 28A — Contract Lines backfill (re-runnable, idempotent, raporlu). Migration Up() ile AYNI SQL'i çalıştırır;
// ops/doğrulama için ayrıca elle koşulabilir. Her sözleşme → 1 line; Payment/ClassBooking → contract'ın line'ı;
// MakeupCreditTransaction → booking üzerinden line; MakeupRequest → eşleşen aktif line; bağlanamayan makeup ledger →
// soft-delete. Kullanım: node scripts/sprint28a-backfill.mjs   (postgres container çalışır olmalı)
import { execFileSync } from "node:child_process";

// Postgres container'ı otomatik bul (standalone veya compose). PGC env ile override edilebilir.
function detectPgc() {
  if (process.env.PGC) return process.env.PGC;
  const names = execFileSync("docker", ["ps", "--format", "{{.Names}}"], { encoding: "utf8" }).split("\n");
  return names.find((n) => /pliaforma-postgres|pliaforma-dev-postgres/.test(n))?.trim() || "pliaforma-postgres";
}
const PGC = detectPgc();
const sql = (q) =>
  execFileSync("docker", ["exec", PGC, "psql", "-U", "postgres", "-d", "pliaforma", "-tAc", q], { encoding: "utf8" }).trim();
const n = (q) => parseInt(sql(q) || "0", 10);

const BACKFILL = `
INSERT INTO "StudentContractLines"
  ("Id","StudioId","StudentContractId","ClassTypeId","InstructorProfileId","StartDate","EndDate",
   "LessonCount","PricePerLesson","TotalPrice","MakeupAllowance","DueDate","IsIndividual","Status",
   "Currency","SequenceNo","Notes","CreatedByUserId","CreatedAt","UpdatedAt","IsDeleted")
SELECT gen_random_uuid(), c."StudioId", c."Id", c."ClassTypeId", c."InstructorProfileId", c."StartDate", c."EndDate",
   c."TotalLessonCount",
   CASE WHEN c."TotalLessonCount" > 0 THEN round((c."Price" + COALESCE(amd.add,0)) / c."TotalLessonCount", 2) ELSE 0 END,
   c."Price" + COALESCE(amd.add,0), c."MakeupAllowance", NULL, c."IsIndividualDefault", c."Status",
   c."Currency", 1, NULL, c."CreatedByUserId", now(), NULL, false
FROM "StudentContracts" c
LEFT JOIN (SELECT "StudentContractId" sc, SUM("AdditionalPrice") add FROM "ContractAmendments" WHERE "IsDeleted"=false GROUP BY "StudentContractId") amd ON amd.sc = c."Id"
WHERE c."IsDeleted" = false
  AND NOT EXISTS (SELECT 1 FROM "StudentContractLines" l WHERE l."StudentContractId" = c."Id" AND l."IsDeleted"=false);

UPDATE "Payments" p SET "StudentContractLineId" = l."Id"
FROM "StudentContractLines" l
WHERE l."StudentContractId" = p."StudentContractId"
  AND p."StudentContractId" IS NOT NULL AND p."StudentContractLineId" IS NULL AND l."IsDeleted"=false;

UPDATE "ClassBookings" b SET "StudentContractLineId" = l."Id"
FROM "StudentContractLines" l
WHERE l."StudentContractId" = b."StudentContractId"
  AND b."StudentContractId" IS NOT NULL AND b."StudentContractLineId" IS NULL AND l."IsDeleted"=false;

UPDATE "MakeupCreditTransactions" t SET "StudentContractLineId" = sub.line
FROM (
  SELECT t2."Id" tid, COALESCE(bs."StudentContractLineId", bu."StudentContractLineId") line
  FROM "MakeupCreditTransactions" t2
  LEFT JOIN "ClassBookings" bs ON bs."Id" = t2."SourceBookingId"
  LEFT JOIN "ClassBookings" bu ON bu."Id" = t2."UsedForBookingId"
  WHERE t2."StudentContractLineId" IS NULL AND t2."IsDeleted"=false
) sub
WHERE t."Id" = sub.tid AND sub.line IS NOT NULL;

UPDATE "MakeupRequests" r SET "StudentContractLineId" = (
  SELECT l."Id" FROM "StudentContractLines" l
  JOIN "StudentContracts" c ON c."Id" = l."StudentContractId"
  WHERE c."StudentProfileId" = r."StudentProfileId"
    AND l."ClassTypeId" = r."ClassTypeId" AND l."InstructorProfileId" = r."RequestedInstructorProfileId"
    AND l."Status" = 'Active' AND l."IsDeleted"=false
  ORDER BY l."EndDate" ASC LIMIT 1)
WHERE r."StudentContractLineId" IS NULL AND r."IsDeleted"=false;

UPDATE "MakeupCreditTransactions" SET "IsDeleted" = true, "UpdatedAt" = now()
WHERE "StudentContractLineId" IS NULL AND "IsDeleted" = false;
`;

const before = {
  ledgerUnlinked: n(`SELECT count(*) FROM "MakeupCreditTransactions" WHERE "StudentContractLineId" IS NULL AND "IsDeleted"=false;`),
};

sql(BACKFILL);

const rep = {
  contracts: n(`SELECT count(*) FROM "StudentContracts" WHERE "IsDeleted"=false;`),
  lines: n(`SELECT count(*) FROM "StudentContractLines" WHERE "IsDeleted"=false;`),
  payLinked: n(`SELECT count(*) FROM "Payments" WHERE "StudentContractId" IS NOT NULL AND "StudentContractLineId" IS NOT NULL;`),
  payTotal: n(`SELECT count(*) FROM "Payments" WHERE "StudentContractId" IS NOT NULL;`),
  bookLinked: n(`SELECT count(*) FROM "ClassBookings" WHERE "StudentContractId" IS NOT NULL AND "StudentContractLineId" IS NOT NULL;`),
  bookTotal: n(`SELECT count(*) FROM "ClassBookings" WHERE "StudentContractId" IS NOT NULL;`),
  ledgerLinked: n(`SELECT count(*) FROM "MakeupCreditTransactions" WHERE "StudentContractLineId" IS NOT NULL AND "IsDeleted"=false;`),
  ledgerSoftDeleted: n(`SELECT count(*) FROM "MakeupCreditTransactions" WHERE "StudentContractLineId" IS NULL AND "IsDeleted"=true;`),
  reqLinked: n(`SELECT count(*) FROM "MakeupRequests" WHERE "StudentContractLineId" IS NOT NULL;`),
  reqTotal: n(`SELECT count(*) FROM "MakeupRequests" WHERE "IsDeleted"=false;`),
};

console.log("== Sprint 28A backfill raporu ==");
console.log(`  Sözleşme (header): ${rep.contracts}  →  Line: ${rep.lines}  (her contract = 1 line)`);
console.log(`  Payment (contract'lı) line'a bağlı: ${rep.payLinked}/${rep.payTotal}`);
console.log(`  ClassBooking (contract'lı) line'a bağlı: ${rep.bookLinked}/${rep.bookTotal}`);
console.log(`  Makeup ledger line'a bağlanan (aktif): ${rep.ledgerLinked}`);
console.log(`  Makeup ledger bağlanamayan → soft-delete (kümülatif): ${rep.ledgerSoftDeleted}  (bu koşuda yeni: ${before.ledgerUnlinked})`);
console.log(`  MakeupRequest line'a bağlı: ${rep.reqLinked}/${rep.reqTotal}`);
console.log("BACKFILL TAMAM ✅");
