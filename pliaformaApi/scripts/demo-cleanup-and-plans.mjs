// Demo cleanup — DB'de YALNIZ 2 demo stüdyo (Evrim Pilates + Aşk Pilates) + 1 platform admin kalır.
// Diğer TÜM stüdyolar/kullanıcılar/veri silinir. Abonelik planları tam 3'e indirilir + max hoca/öğrenci limitleri.
// Kullanım: node scripts/demo-cleanup-and-plans.mjs   (backend http://localhost:5132 ayakta, postgres container çalışır)
import { execFileSync } from "node:child_process";

const BASE = process.env.BASE || "http://localhost:5132";
const PW = "Parola123";
const PGC = "pliaforma-postgres";
const ADMIN = "admin@pliaforma.local";
const KEEP_USERS = [
  "evrim@evrimpilates.local", "seyda@evrimpilates.local",
  "dilara@askpilates.local", "ahmet@askpilates.local",
];

const sql = (q) =>
  execFileSync("docker", ["exec", PGC, "psql", "-U", "postgres", "-d", "pliaforma", "-tAc", q], { encoding: "utf8" }).trim();
const inList = (arr) => arr.map((e) => `'${e}'`).join(",");

async function api(path, body, token) {
  const r = await fetch(BASE + path, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: "Bearer " + token } : {}) },
    body: JSON.stringify(body ?? {}),
  });
  return { status: r.status, text: await r.text() };
}

console.log("== Önce: stüdyo / kullanıcı / plan sayıları ==");
console.log("  " + sql(`SELECT 'studyo='||count(*) FROM "Studios";`) + " | " +
  sql(`SELECT 'kullanici='||count(*) FROM "Users";`) + " | " +
  sql(`SELECT 'plan='||count(*) FROM "SubscriptionPlans";`));

// 1) Demo dışı TÜM stüdyoları sil (cascade: üyelik/profil/öğrenci/abonelik/seans... hepsi gider).
sql(`DELETE FROM "Studios" WHERE "OwnerUserId" NOT IN (SELECT "Id" FROM "Users" WHERE "Email" IN (${inList(KEEP_USERS)}));`);
console.log("✓ Demo dışı stüdyolar silindi");

// 2) Demo 4 kullanıcı DIŞINDA tüm kullanıcıları sil (eski platform admin'ler dahil → temiz admin yeniden kurulur).
sql(`DELETE FROM "Users" WHERE "Email" NOT IN (${inList(KEEP_USERS)});`);
console.log("✓ Demo dışı kullanıcılar silindi (eski platform admin'ler dahil)");

// 3) Temiz platform admin kur: kayıt (API) + GlobalRole + e-posta doğrulanmış.
const reg = await api("/api/auth/register", { fullName: "Platform Admin", email: ADMIN, password: PW });
if (reg.status !== 200 && reg.status !== 201) throw new Error(`admin register başarısız: ${reg.status} ${reg.text}`);
sql(`UPDATE "Users" SET "GlobalRole"='PlatformAdmin', "EmailVerifiedAt"=now() WHERE "Email"='${ADMIN}';`);
console.log(`✓ Platform admin: ${ADMIN} / ${PW}`);

// 4) Abonelik planları → TAM 3 (Pro / Pro+ / Enterprise). Diğerleri (Başlangıç + S13 Test) silinir.
//    PlatformSubscription→Plan FK SET NULL olduğundan plan silmek aboneliği bozmaz; demo stüdyolar zaten Pro (default).
sql(`DELETE FROM "SubscriptionPlans" WHERE "Tier" NOT IN ('Pro','Pro+','Enterprise');`);
// Max hoca/öğrenci limitleri (mantıklı kademe; bu sürümde gösterim amaçlı, zorlama yok).
sql(`UPDATE "SubscriptionPlans" SET "MaxInstructors"=5,   "MaxStudents"=200,  "DisplayOrder"=1 WHERE "Tier"='Pro';`);
sql(`UPDATE "SubscriptionPlans" SET "MaxInstructors"=15,  "MaxStudents"=750,  "DisplayOrder"=2 WHERE "Tier"='Pro+';`);
sql(`UPDATE "SubscriptionPlans" SET "MaxInstructors"=100, "MaxStudents"=5000, "DisplayOrder"=3 WHERE "Tier"='Enterprise';`);
console.log("✓ Abonelik planları 3'e indirildi + max hoca/öğrenci limitleri ayarlandı");

console.log("\n== Sonra: kalan kayıtlar ==");
console.log("  " + sql(`SELECT 'studyo='||count(*) FROM "Studios";`) + " | " +
  sql(`SELECT 'kullanici='||count(*) FROM "Users";`) + " | " +
  sql(`SELECT 'plan='||count(*) FROM "SubscriptionPlans";`));
console.log(`\n\x1b[36mTEMİZLİK TAMAM ✅\x1b[0m`);
console.log(`  Platform admin: ${ADMIN} / ${PW}`);
