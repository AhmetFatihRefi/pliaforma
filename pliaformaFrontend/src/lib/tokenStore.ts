// Access token BELLEKTE tutulur (localStorage'a yazılmaz — XSS yüzeyini küçültür).
// Refresh token ise backend tarafından httpOnly cookie ile yönetilir; JS görmez.
// Aktif stüdyo id'si hassas değildir; sayfa yenilemede refresh'e bağlam vermek için
// localStorage'da saklanır.

let accessToken: string | null = null;
const ACTIVE_STUDIO_KEY = "pf_active_studio";

export const tokenStore = {
  getAccessToken: () => accessToken,
  setAccessToken: (token: string | null) => {
    accessToken = token;
  },

  getActiveStudioId: (): string | null =>
    typeof window === "undefined" ? null : window.localStorage.getItem(ACTIVE_STUDIO_KEY),

  setActiveStudioId: (id: string | null) => {
    if (typeof window === "undefined") return;
    if (id) window.localStorage.setItem(ACTIVE_STUDIO_KEY, id);
    else window.localStorage.removeItem(ACTIVE_STUDIO_KEY);
  },

  clear: () => {
    accessToken = null;
    if (typeof window !== "undefined") window.localStorage.removeItem(ACTIVE_STUDIO_KEY);
  },
};
