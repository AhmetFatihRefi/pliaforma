// Typed API client — web lib/api.ts deseninin mobil eşleniği: envelope çözme + bearer + tekil-uçuş
// refresh + istek başına EN FAZLA 1 retry. Cookie/credentials yok; token asla loglanmaz.
import { refreshSession } from '../auth/refresh';
import { session } from '../auth/session';
import { ApiError, rawRequest, safeMessage } from './http';

interface ApiOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  /** false → Authorization eklenmez ve 401'de refresh denenmez (anonim auth uçları). */
  auth?: boolean;
}

export async function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  const { method = 'GET', body, auth = true } = options;

  if (auth && session.isExpired()) {
    // Proaktif yenileme: süre dolmuşsa 401 beklemeden tek uçuşta tazele.
    await refreshSession();
  }

  const attempt = () =>
    rawRequest<T>(path, {
      method,
      body: body === undefined ? undefined : JSON.stringify(body),
      headers: auth && session.token ? { Authorization: `Bearer ${session.token}` } : {},
    });

  let { status, envelope } = await attempt();

  if (status === 401 && auth) {
    // Pasif yol: refresh (tekil-uçuş) + orijinal isteği BİR kez tekrarla. Refresh 401 verirse
    // refreshSession oturumu düşürür ve fırlatır — retry döngüsü oluşamaz.
    await refreshSession();
    ({ status, envelope } = await attempt());
  }

  if (status >= 200 && status < 300 && envelope?.success) {
    return envelope.data as T;
  }
  throw new ApiError(status, safeMessage(status, envelope?.message ?? null), envelope?.errors ?? null);
}

export { ApiError };
