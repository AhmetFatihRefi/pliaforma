# ADR-004 — Platform faturalama modeli

**Durum:** Açık (ürün sahibiyle netleştirilecek; geç sprint)

**Bağlam:** Platform stüdyodan ücret alır. Ürün sahibi "hoca başına" düşündü ama paket de olabilir
dedi. Öğrenciden platform ücreti ALINMAZ.

**Öneri:** Saf hoca-başı (per-seat) yerine **kademeli paket** (tier başına N aktif hocaya kadar) +
üzerine hoca-başı taşma. Daha öngörülebilir gelir, stüdyo için sindirilebilir fiyat.

**İki ayrı para akışı (karıştırma):**
1. **Hoca ders ücreti** — stüdyo içi, `InstructorProfile.Rate`; hoca gelir panelinin temeli.
2. **Platform aboneliği** — stüdyo→platform; aktif `Instructor` üyelik sayısına göre.

**Açık sorular:** fiyat kademeleri, deneme süresi, stüdyo↔öğrenci ders ödemelerinin (sıklık/gün)
yalnızca panel için mi tutulacağı (ödeme sprintinde teyit).
