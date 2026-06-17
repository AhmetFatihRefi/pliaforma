import type { ApiEnvelope, AuthResponse } from "./types";
import { tokenStore } from "./tokenStore";

// Same-origin: varsayılan BOŞ → istekler relative `/api/...` gider (Next rewrite veya nginx proxy'ler).
// NEXT_PUBLIC_API_URL yalnızca istisnai doğrudan-erişim (CORS) senaryosu için tam URL'e ayarlanabilir.
const BASE_URL = process.env.NEXT_PUBLIC_API_URL || "";

export class ApiError extends Error {
  status: number;
  errors?: Record<string, string[]> | null;

  constructor(status: number, message: string, errors?: Record<string, string[]> | null) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  /** true ise 401'de refresh denenmez (refresh/login isteğinin kendisi gibi). */
  skipAuthRetry?: boolean;
}

/** Eşzamanlı 401'lerde tek bir refresh çağrısı yapılmasını sağlar (single-flight). */
let refreshInFlight: Promise<boolean> | null = null;

function refreshOnce(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const res = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ studioId: tokenStore.getActiveStudioId() }),
      });
      if (!res.ok) return false;
      const env = (await res.json()) as ApiEnvelope<AuthResponse>;
      if (!env.success || !env.data) return false;
      tokenStore.setAccessToken(env.data.accessToken);
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const doFetch = async (): Promise<Response> => {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const token = tokenStore.getAccessToken();
    if (token) headers.Authorization = `Bearer ${token}`;

    return fetch(`${BASE_URL}${path}`, {
      method: options.method ?? "GET",
      headers,
      credentials: "include",
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    });
  };

  let res = await doFetch();

  // 401 → tek seferlik refresh + tekrar dene.
  if (res.status === 401 && !options.skipAuthRetry) {
    const refreshed = await refreshOnce();
    if (refreshed) res = await doFetch();
  }

  const env = (await res.json().catch(() => null)) as ApiEnvelope<T> | null;

  if (!res.ok || !env || !env.success) {
    throw new ApiError(res.status, env?.message ?? "Beklenmeyen bir hata oluştu.", env?.errors);
  }

  return env.data as T;
}
