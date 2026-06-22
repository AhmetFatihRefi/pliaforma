import { ApiError } from "./api";

// Kullanıcıya ASLA teknik detay/GUID göstermeyiz. Sunucu zaten iş kurallarını kullanıcı diliyle (Türkçe) döndürür;
// 5xx'te genel mesaj verir ve gerçek istisnayı sunucu loglarına yazar (admin/geliştirici görür). Burada ek güvenlik:
// 5xx → genel mesaj, içinde GUID geçen bir mesaj → genel mesaj (sızıntı olmasın).
const GUID_RE = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;

/** Bir hatadan kullanıcıya gösterilebilir, dile uygun, teknik-detaysız mesaj üretir. */
export function errorMessage(e: unknown, fallback = "İşlem tamamlanamadı. Lütfen tekrar deneyin."): string {
  if (e instanceof ApiError) {
    if (e.status >= 500) return "Beklenmedik bir hata oluştu. Lütfen tekrar deneyin.";
    const msg = (e.message ?? "").trim();
    if (!msg || GUID_RE.test(msg)) return fallback;
    return msg;
  }
  return fallback;
}
