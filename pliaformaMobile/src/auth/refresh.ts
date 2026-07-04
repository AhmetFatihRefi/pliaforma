// Tekil-uçuş (single-flight) refresh: aynı anda TEK refresh isteği; bekleyenler aynı promise'i paylaşır.
// Refresh YALNIZ /api/auth/mobile/refresh kullanır (web /api/auth/refresh mobilde YASAK — kanal bağlama).
import { ApiError, rawRequest, safeMessage, TransientError } from '../api/http';
import type { MobileAuthResponse } from '../api/types';
import { session } from './session';
import { tokenStore } from './tokenStore';

let inflight: Promise<MobileAuthResponse> | null = null;

export function refreshSession(): Promise<MobileAuthResponse> {
  if (!inflight) {
    inflight = doRefresh().finally(() => {
      inflight = null;
    });
  }
  return inflight;
}

async function doRefresh(): Promise<MobileAuthResponse> {
  const refreshToken = await tokenStore.getRefreshToken();
  if (!refreshToken) {
    session.clear();
    session.emitSessionLost();
    throw new ApiError(401, 'Oturum bulunamadı. Lütfen tekrar giriş yapın.');
  }

  const { status, envelope } = await rawRequest<MobileAuthResponse>('/api/auth/mobile/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken, studioId: session.studioId ?? undefined }),
  });

  // F1 (H1 düzeltmesi) — hata AYRIMI:
  //  * 401/403 = token GERÇEKTEN reddedildi (rotasyon/iptal/süre) → yerel oturum tamamen düşürülür.
  //  * 429 / 5xx / parse edilemeyen yanıt = GEÇİCİ (rate-limit, deploy penceresi, bozuk cevap) →
  //    token'lara DOKUNULMAZ; TransientError fırlar (bootstrap 'offline'a düşer, çağıran yeniden dener).
  //  * Gerçek ağ hatası zaten fetch'ten fırlar ve buraya hiç gelmez (token korunur).
  if (status === 401 || status === 403) {
    await tokenStore.clearAll();
    session.clear();
    session.emitSessionLost();
    throw new ApiError(status, 'Oturum süresi doldu. Lütfen tekrar giriş yapın.');
  }
  if (status !== 200 || !envelope?.success || !envelope.data) {
    throw new TransientError();
  }

  const data = envelope.data;
  // Rotasyon kuralı: yeni refresh token ÖNCE SecureStore'a (kalıcı), SONRA access token belleğe.
  await tokenStore.setRefreshToken(data.refreshToken);
  session.setAccess(data.accessToken, data.accessTokenExpiresAtUnix);
  session.setStudioId(data.activeStudio?.studioId ?? session.studioId);
  return data;
}

export { safeMessage };
