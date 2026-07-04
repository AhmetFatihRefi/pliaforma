// Taban HTTP yardımcıları — hem client.ts hem refresh.ts kullanır (döngüsel import olmasın diye ayrı).
// Kurallar (mobil AGENTS.md): cookie YOK, `credentials: 'include'` YOK; token/parola/yanıt LOGLANMAZ.
import type { ApiEnvelope } from './types';

// Public origin — secret DEĞİLDİR. Dev'de EXPO_PUBLIC_API_URL ile lokal backend'e yönlendirilebilir
// (örn. http://192.168.1.20:5132). EXPO_PUBLIC_* içine ASLA secret konulmaz (bundle'a gömülür).
export const BASE_URL = (process.env.EXPO_PUBLIC_API_URL ?? 'https://pliaforma.com').replace(/\/+$/, '');

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly errors: Record<string, string[]> | null = null,
  ) {
    super(message);
  }
}

/**
 * GEÇİCİ hata (F1): 429 / 5xx / parse hatası / ağ sorunu — oturum GEÇERSİZ demek DEĞİLDİR.
 * Bu hatada token'lara asla dokunulmaz; kullanıcı yeniden dener (bootstrap → offline/retry).
 * ApiError'dan bilinçli olarak AYRI tiptir: catch bloklarındaki `instanceof ApiError` ayrımı
 * "oturum reddedildi" ile "sunucuya ulaşılamadı/yoğun"u ayırt eder.
 */
export class TransientError extends Error {
  constructor(message = 'Sunucuya şu anda ulaşılamıyor. Lütfen tekrar deneyin.') {
    super(message);
  }
}

/** 5xx / beklenmedik hata → kullanıcıya sade Türkçe; teknik detay UI'a sızmaz. */
export function safeMessage(status: number, message: string | null): string {
  if (status >= 500 || !message) return 'Bir sorun oluştu. Lütfen tekrar deneyin.';
  return message;
}

export interface RawResult<T> {
  status: number;
  envelope: ApiEnvelope<T> | null;
}

/** Düşük seviyeli istek: envelope'u parse eder, HATA FIRLATMAZ (karar çağıranın). */
export async function rawRequest<T>(path: string, init: RequestInit): Promise<RawResult<T>> {
  const res = await fetch(BASE_URL + path, {
    ...init,
    // F6.b: RN'nin native cookie jar'ı hiçbir isteğe cookie eklemesin/saklamasın (savunma katmanı;
    // mobil auth zaten bearer + body tabanlıdır ve backend mobil uçları cookie yazmaz).
    credentials: 'omit',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...(init.headers ?? {}) },
  });
  let envelope: ApiEnvelope<T> | null = null;
  try {
    envelope = (await res.json()) as ApiEnvelope<T>;
  } catch {
    envelope = null;
  }
  return { status: res.status, envelope };
}
