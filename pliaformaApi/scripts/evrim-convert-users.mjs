// Evrim demo — tüm öğrencileri (UserId'siz) user'a dönüştürür. Ad-soyaddan e-posta üretir (Türkçe sadeleştirilir).
// Kullanım: node evrim-convert-users.mjs [BASE] [PW]
const BASE = process.argv[2] || "http://localhost:5132";
const PW = process.argv[3] || "Demo!2345";
const tr = { "ş": "s", "Ş": "s", "ç": "c", "Ç": "c", "ı": "i", "İ": "i", "ğ": "g", "Ğ": "g", "ü": "u", "Ü": "u", "ö": "o", "Ö": "o" };
const slug = (s) => s.split("").map((c) => tr[c] ?? c).join("").toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, ".").replace(/^\.|\.$/g, "");

const login = async (email) => {
  const r = await fetch(BASE + "/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password: PW }) });
  return (await r.json()).data.accessToken;
};

(async () => {
  const tok = await login("evrim@evrimmutlupilates.local");
  const sr = await fetch(BASE + "/api/studio/students", { headers: { Authorization: "Bearer " + tok } });
  const students = (await sr.json()).data;
  const used = new Set();
  let done = 0, skip = 0, fail = 0;
  for (const s of students) {
    if (s.userId) { skip++; continue; }
    let local = slug(s.fullName); if (used.has(local)) local = local + "." + s.id.slice(0, 4); used.add(local);
    const email = local + "@demo.local";
    const r = await fetch(BASE + "/api/studio/students/" + s.id + "/convert-to-user", {
      method: "POST", headers: { "Content-Type": "application/json", Authorization: "Bearer " + tok },
      body: JSON.stringify({ email, initialPassword: PW }),
    });
    if (r.ok) { done++; console.log("  OK", s.fullName, "->", email); }
    else { fail++; const e = await r.json().catch(() => ({})); console.log("  XX", s.fullName, r.status, e.message || ""); }
  }
  console.log(`\nDönüştürülen: ${done} · zaten user: ${skip} · hata: ${fail}`);
})();
