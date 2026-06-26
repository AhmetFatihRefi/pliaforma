// Sprint 41: Tek noktadan TR tarih gösterimi.
// ÖNEMLİ: Burada bilinçli olarak Date'e parse + yerel saate kaydırma YAPILMAZ. ISO/DateOnly
// string'in gün ve saat bileşenleri olduğu gibi alınıp "GG.AA.YYYY" biçimine çevrilir — yani
// mevcut `iso.slice(...)` davranışıyla AYNI bileşenler gösterilir, timezone semantiği değişmez.
// (Backend zaten istenen saat dilimini string içinde döndürüyor; tz dönüşümü ayrı bir iş.)

const DASH = "—";

/** "2026-06-26" veya "2026-06-26T14:30:00Z" → "26.06.2026". Boş/geçersizde "—" / ham string. */
export function formatDate(iso?: string | null): string {
  if (!iso) return DASH;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  return m ? `${m[3]}.${m[2]}.${m[1]}` : iso;
}

/** "2026-06-26T14:30:00Z" → "26.06.2026 14:30". Saat yoksa yalnız tarih döner. */
export function formatDateTime(iso?: string | null): string {
  if (!iso) return DASH;
  const date = formatDate(iso);
  if (date === iso || date === DASH) return date;
  const time = iso.slice(11, 16); // HH:MM (tz dönüşümü yok — string'den)
  return /^\d{2}:\d{2}$/.test(time) ? `${date} ${time}` : date;
}
