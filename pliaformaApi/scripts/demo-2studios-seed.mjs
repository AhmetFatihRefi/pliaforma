// Demo seed — 2 stüdyo (Evrim Pilates + Aşk Pilates), sahibi-hoca + 2. hoca + kadın öğrenciler.
// "Bugün kayıt olmuş gibi" sıfırdan: ders/sözleşme/seans YOK (sonra atanacak).
// Türkçe karakterler için Node fetch (UTF-8). E-posta verify + owner-instructor insert için psql (execFileSync).
// Kullanım: node scripts/demo-2studios-seed.mjs   (backend http://localhost:5132 ayakta olmalı, postgres container çalışmalı)
import { execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";

const BASE = process.env.BASE || "http://localhost:5132";
const PW = "Parola123!";
const PGC = "pliaforma-postgres";

const sql = (q) =>
  execFileSync("docker", ["exec", PGC, "psql", "-U", "postgres", "-d", "pliaforma", "-tAc", q], { encoding: "utf8" }).trim();

async function api(path, body, token) {
  const r = await fetch(BASE + path, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: "Bearer " + token } : {}) },
    body: JSON.stringify(body ?? {}),
  });
  const text = await r.text();
  return { status: r.status, text };
}
const field = (text, key) => (text.match(new RegExp(`"${key}":"([^"]+)"`)) || [])[1];

async function login(email) {
  const { text } = await api("/api/auth/login", { email, password: PW });
  return field(text, "accessToken");
}

// Bir stüdyo kur: sahibi-hoca + 1 normal hoca + kadın öğrenciler.
async function seedStudio({ studioName, owner, instructor, students, domain }) {
  console.log(`\n=== ${studioName} ===`);

  // 1) Sahip + stüdyo (bugün kayıt). Owner token döner.
  const reg = await api("/api/auth/register-studio", {
    studioName, ownerFullName: owner.fullName, email: owner.email, password: PW,
  });
  const ownerToken = field(reg.text, "accessToken");
  if (!ownerToken) throw new Error(`register-studio başarısız (${studioName}): ${reg.status} ${reg.text}`);
  console.log(`  ✓ Sahip-hoca: ${owner.fullName} <${owner.email}>`);

  // 2) Sahibe InstructorProfile ekle (Owner rolü otomatik almıyor) → hoca olarak atanabilir. İdempotent.
  sql(
    `INSERT INTO "InstructorProfiles" ("Id","StudioId","UserId","DisplayName","Rate","RateType","IsAcceptingStudents","CreatedAt","IsDeleted") ` +
    `SELECT gen_random_uuid(), s."Id", u."Id", u."FullName", 0, 'PerSession', true, now(), false ` +
    `FROM "Users" u JOIN "Studios" s ON s."OwnerUserId"=u."Id" ` +
    `WHERE u."Email"='${owner.email}' ` +
    `AND NOT EXISTS (SELECT 1 FROM "InstructorProfiles" ip WHERE ip."StudioId"=s."Id" AND ip."UserId"=u."Id");`
  );
  console.log(`  ✓ Sahibe hoca profili eklendi (ders atanabilir)`);

  // 3) 2. hoca (normal): kayıt + davet + e-posta doğrula + kabul.
  await api("/api/auth/register", { fullName: instructor.fullName, email: instructor.email, password: PW });
  const inv = await api("/api/studio/memberships/invite", { email: instructor.email, role: "Instructor" }, ownerToken);
  const membershipId = field(inv.text, "membershipId");
  if (!membershipId) throw new Error(`invite başarısız (${instructor.email}): ${inv.status} ${inv.text}`);
  sql(`UPDATE "Users" SET "EmailVerifiedAt"=now() WHERE "Email"='${instructor.email}' AND "EmailVerifiedAt" IS NULL;`);
  const instrToken = await login(instructor.email);
  const acc = await api(`/api/members/${membershipId}/accept`, {}, instrToken);
  if (acc.status !== 200) throw new Error(`accept başarısız (${instructor.email}): ${acc.status} ${acc.text}`);
  console.log(`  ✓ 2. hoca: ${instructor.fullName} <${instructor.email}>`);

  // 4) Kadın öğrenciler (misafir; User'sız). Ders/sözleşme yok.
  const sources = ["Phone", "WalkIn", "Referral", "Other", "Web"];
  let n = 0;
  for (const name of students) {
    const res = await api("/api/studio/students/guest",
      { fullName: name, source: sources[n % sources.length] }, ownerToken);
    if (res.status !== 200) throw new Error(`öğrenci oluşturulamadı (${name}): ${res.status} ${res.text}`);
    n++;
  }
  console.log(`  ✓ ${n} kadın öğrenci eklendi`);
  return { studioName, owner: owner.email, instructor: instructor.email, students: n };
}

// --- Kadın isimleri (Türkçe karakterli — UTF-8 doğrulaması) ---
const studentsEvrim = [
  "Zeynep Yılmaz", "Elif Demir", "Ayşe Kaya", "Fatma Şahin", "Emine Çelik",
  "Hatice Yıldız", "Merve Arslan", "Büşra Doğan", "Esra Aydın", "Derya Koç",
  "Selin Öztürk", "Ceren Aksoy", "Gizem Polat", "Pınar Korkmaz", "Ebru Çetin",
];
const studentsAsk = [
  "Sıla Erdoğan", "Damla Aslan", "Nazlı Güneş", "Yağmur Kurt", "Ela Bulut",
  "İrem Aydın", "Melike Şen", "Tuğçe Yavuz", "Aslı Bozkurt", "Burcu Çakır",
  "Dilan Acar", "Eda Karaca", "Funda Yıldırım", "Gül Şimşek", "Hande Yalçın",
  "Irmak Özdemir", "Kübra Avcı", "Leyla Taş",
];

// --- Reset (idempotent): yalnız bu iki demo domain'i temizle ---
console.log("== Reset (yalnız @evrimpilates.local + @askpilates.local) ==");
sql(`DELETE FROM "Studios" WHERE "OwnerUserId" IN (SELECT "Id" FROM "Users" WHERE "Email" LIKE '%@evrimpilates.local' OR "Email" LIKE '%@askpilates.local');`);
sql(`DELETE FROM "Users" WHERE "Email" LIKE '%@evrimpilates.local' OR "Email" LIKE '%@askpilates.local';`);
console.log("  ✓ Önceki demo verisi temizlendi");

const r1 = await seedStudio({
  studioName: "Evrim Pilates",
  owner: { fullName: "Evrim", email: "evrim@evrimpilates.local" },
  instructor: { fullName: "Şeyda Kantarcı", email: "seyda@evrimpilates.local" },
  students: studentsEvrim,
});
const r2 = await seedStudio({
  studioName: "Aşk Pilates",
  owner: { fullName: "Dilara Dikici", email: "dilara@askpilates.local" },
  instructor: { fullName: "Ahmet Fatih Refi", email: "ahmet@askpilates.local" },
  students: studentsAsk,
});

console.log(`\n\x1b[36mDEMO SEED TAMAM ✅\x1b[0m  (tüm parolalar: ${PW})`);
console.log(`  ${r1.studioName}: sahip-hoca ${r1.owner} · 2. hoca ${r1.instructor} · ${r1.students} öğrenci`);
console.log(`  ${r2.studioName}: sahip-hoca ${r2.owner} · 2. hoca ${r2.instructor} · ${r2.students} öğrenci`);
