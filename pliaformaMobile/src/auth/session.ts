// Bellek-içi oturum: access token asla diske yazılmaz (mobil AGENTS.md). App restart → refresh ile yenilenir.
// Expiry hesabı K6/M3 kuralı: YALNIZ epoch (Unix) alanları + JWT `exp` claim'i; DateTime string parse YASAK.

let accessToken: string | null = null;
let accessExpUnix: number | null = null;
let activeStudioId: string | null = null;

const lostListeners = new Set<() => void>();

/** JWT payload'ından exp (epoch saniye) okur — Unix alanı olmayan yanıtlar (switch-studio) için tek kaynak. */
export function jwtExpUnix(token: string): number | null {
  try {
    // F6.a: base64url → base64 + PAD — katı atob implementasyonları pad'siz girdide fırlatır;
    // parse edilemezse null döner ve expiry kararı 401-yoluna bırakılır (güvenli fallback).
    let payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    payload += '='.repeat((4 - (payload.length % 4)) % 4);
    const exp = (JSON.parse(globalThis.atob(payload)) as { exp?: number }).exp;
    return typeof exp === 'number' ? exp : null;
  } catch {
    return null;
  }
}

export const session = {
  /** unix verilmezse (web AuthResponse) JWT exp'ten türetilir. */
  setAccess(token: string, unix?: number | null) {
    accessToken = token;
    accessExpUnix = unix && unix > 0 ? unix : jwtExpUnix(token);
  },
  setStudioId(id: string | null) {
    activeStudioId = id;
  },
  clear() {
    accessToken = null;
    accessExpUnix = null;
    activeStudioId = null;
  },
  get token() {
    return accessToken;
  },
  get studioId() {
    return activeStudioId;
  },
  /** 30 sn pay ile: süresi dolmuş/dolmak üzereyse proaktif refresh tetiklenir. */
  isExpired(): boolean {
    if (!accessToken) return true;
    if (accessExpUnix == null) return false; // exp okunamadıysa 401'e bırak
    return Date.now() / 1000 >= accessExpUnix - 30;
  },
  /** Refresh kalıcı olarak başarısız olduğunda (oturum düştü) AuthContext'i uyandırır. */
  onSessionLost(fn: () => void): () => void {
    lostListeners.add(fn);
    return () => lostListeners.delete(fn);
  },
  emitSessionLost() {
    for (const fn of lostListeners) fn();
  },
};
