// Auth state machine (SPRINT-54-SPEC §5 — bağlayıcı): bootstrap → refresh; login üç dallı
// (challenge / setup / normal); studio seçimi koşullu; logout tam temizlik. Parola hiçbir yerde
// saklanmaz; token/auth yanıtı loglanmaz.
import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { api } from '../api/client';
import { ApiError, rawRequest, safeMessage } from '../api/http';
import type {
  ActiveStudioDto,
  MembershipDto,
  MfaSetupStartResponse,
  MobileAuthResponse,
  MobileLoginData,
  MobileMfaSetupCompleteResponse,
  SwitchStudioResponse,
  UserDto,
} from '../api/types';
import { isMfaChallenge } from '../api/types';
import { refreshSession } from './refresh';
import { session } from './session';
import { tokenStore } from './tokenStore';

export type AuthStatus =
  | 'bootstrapping'
  | 'offline' // bootstrap ağ hatası: token SİLİNMEZ, yeniden dene
  | 'signedOut'
  | 'mfaChallenge'
  | 'mfaSetup'
  | 'selectStudio'
  | 'signedIn';

interface AuthState {
  status: AuthStatus;
  user: UserDto | null;
  memberships: MembershipDto[];
  activeStudio: ActiveStudioDto | null;
  challengeToken: string | null;
}

const initial: AuthState = {
  status: 'bootstrapping',
  user: null,
  memberships: [],
  activeStudio: null,
  challengeToken: null,
};

interface AuthApi extends AuthState {
  login(email: string, password: string): Promise<void>;
  verifyMfaCode(code: string, rememberDevice: boolean): Promise<void>;
  verifyRecoveryCode(code: string): Promise<void>;
  /** F2: challenge'dan girişe dönüş — süresi dolmuş/istenmeyen challenge çıkmaz sokak olmasın. */
  cancelChallenge(): void;
  startMfaSetup(): Promise<MfaSetupStartResponse>;
  /** Kod doğrulanır, recovery kodları EKRANA döner (cihaza kaydedilmez); yönlendirme completeMfaSetup ile. */
  verifyMfaSetup(code: string): Promise<string[]>;
  completeMfaSetup(): void;
  selectStudio(studioId: string): Promise<void>;
  logout(): Promise<void>;
  retryBootstrap(): void;
}

const Ctx = createContext<AuthApi | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>(initial);
  // MFA setup doğrulandıktan sonra, kullanıcı recovery kodlarını onaylayana dek bekletilen auth.
  const pendingSetupAuth = useRef<MobileAuthResponse | null>(null);

  /** Başarılı auth yanıtını benimser: önce SecureStore (kalıcı), sonra bellek, sonra state. */
  const adoptAuth = useCallback(async (data: MobileAuthResponse, rememberDevice = false) => {
    await tokenStore.setRefreshToken(data.refreshToken);
    if (rememberDevice && data.trustedDeviceToken) {
      await tokenStore.setTrustedDeviceToken(data.trustedDeviceToken);
    }
    session.setAccess(data.accessToken, data.accessTokenExpiresAtUnix);
    session.setStudioId(data.activeStudio?.studioId ?? null);

    const needsStudio = !data.activeStudio && data.memberships.length > 1;
    setState({
      status: data.mfaSetupRequired ? 'mfaSetup' : needsStudio ? 'selectStudio' : 'signedIn',
      user: data.user,
      memberships: data.memberships,
      activeStudio: data.activeStudio,
      challengeToken: null,
    });
  }, []);

  const toSignedOut = useCallback(() => {
    session.clear();
    setState({ ...initial, status: 'signedOut' });
  }, []);

  const bootstrap = useCallback(async () => {
    setState((s) => ({ ...s, status: 'bootstrapping' }));
    const stored = await tokenStore.getRefreshToken();
    if (!stored) {
      toSignedOut();
      return;
    }
    try {
      const data = await refreshSession(); // rotasyon + SecureStore yazımı refresh.ts içinde
      setState({
        status: data.mfaSetupRequired
          ? 'mfaSetup'
          : !data.activeStudio && data.memberships.length > 1
            ? 'selectStudio'
            : 'signedIn',
        user: data.user,
        memberships: data.memberships,
        activeStudio: data.activeStudio,
        challengeToken: null,
      });
    } catch (e) {
      if (e instanceof ApiError) {
        toSignedOut(); // 401/4xx: storage refresh.ts'te zaten temizlendi
      } else {
        // Ağ hatası: token SİLİNMEZ; kullanıcı tekrar dener (spec §5).
        setState((s) => ({ ...s, status: 'offline' }));
      }
    }
  }, [toSignedOut]);

  useEffect(() => {
    void bootstrap();
    // Uygulama içi herhangi bir anda refresh kalıcı düşerse login'e dön.
    return session.onSessionLost(toSignedOut);
  }, [bootstrap, toSignedOut]);

  const login = useCallback(
    async (email: string, password: string) => {
      const trusted = await tokenStore.getTrustedDeviceToken();
      const { status, envelope } = await rawRequest<MobileLoginData>('/api/auth/mobile/login', {
        method: 'POST',
        body: JSON.stringify({ email, password, trustedDeviceToken: trusted ?? undefined }),
      });
      if (status !== 200 || !envelope?.success || !envelope.data) {
        throw new ApiError(status, safeMessage(status, envelope?.message ?? null), envelope?.errors ?? null);
      }
      const data = envelope.data;
      if (isMfaChallenge(data)) {
        setState((s) => ({ ...s, status: 'mfaChallenge', challengeToken: data.challengeToken }));
        return;
      }
      await adoptAuth(data);
    },
    [adoptAuth],
  );

  const verifyMfaCode = useCallback(
    async (code: string, rememberDevice: boolean) => {
      const data = await api<MobileAuthResponse>('/api/auth/mobile/mfa/challenge/verify', {
        method: 'POST',
        auth: false,
        body: { challengeToken: state.challengeToken, code, rememberDevice },
      });
      await adoptAuth(data, rememberDevice);
    },
    [adoptAuth, state.challengeToken],
  );

  const verifyRecoveryCode = useCallback(
    async (recoveryCode: string) => {
      // Recovery yolu trusted device ÜRETMEZ (backend garanti eder; istemci de istemez).
      const data = await api<MobileAuthResponse>('/api/auth/mobile/mfa/recovery-code/verify', {
        method: 'POST',
        auth: false,
        body: { challengeToken: state.challengeToken, recoveryCode },
      });
      await adoptAuth(data);
    },
    [adoptAuth, state.challengeToken],
  );

  // F2: challenge token bellek-içidir; iptalde yalnız state sıfırlanır (SecureStore'da silinecek şey yok —
  // challenge aşamasında henüz hiçbir token yazılmamıştır).
  const cancelChallenge = useCallback(() => {
    setState({ ...initial, status: 'signedOut' });
  }, []);

  const startMfaSetup = useCallback(
    () => api<MfaSetupStartResponse>('/api/auth/mfa/setup/start', { method: 'POST', body: {} }),
    [],
  );

  const verifyMfaSetup = useCallback(async (code: string) => {
    const r = await api<MobileMfaSetupCompleteResponse>('/api/auth/mobile/mfa/setup/verify', {
      method: 'POST',
      body: { code },
    });
    // Token'ları hemen benimse (güvenli), ama ekran recovery kodlarını gösterip onay alana kadar
    // mfaSetup durumunda kal — kodlar CİHAZA KAYDEDİLMEZ, yalnız bir kez gösterilir.
    pendingSetupAuth.current = r.auth;
    await tokenStore.setRefreshToken(r.auth.refreshToken);
    session.setAccess(r.auth.accessToken, r.auth.accessTokenExpiresAtUnix);
    session.setStudioId(r.auth.activeStudio?.studioId ?? null);
    return r.recoveryCodes;
  }, []);

  const completeMfaSetup = useCallback(() => {
    const auth = pendingSetupAuth.current;
    pendingSetupAuth.current = null;
    if (!auth) return;
    const needsStudio = !auth.activeStudio && auth.memberships.length > 1;
    setState({
      status: needsStudio ? 'selectStudio' : 'signedIn',
      user: auth.user,
      memberships: auth.memberships,
      activeStudio: auth.activeStudio,
      challengeToken: null,
    });
  }, []);

  const selectStudio = useCallback(async (studioId: string) => {
    // Web switch-studio ucu bearer-tabanlı ve cookie'sizdir; refresh token DEĞİŞMEZ.
    const data = await api<SwitchStudioResponse>('/api/auth/switch-studio', {
      method: 'POST',
      body: { studioId },
    });
    session.setAccess(data.accessToken); // Unix alanı yok → JWT exp'ten (session.ts)
    session.setStudioId(data.activeStudio?.studioId ?? studioId);
    setState((s) => ({
      ...s,
      status: 'signedIn',
      user: data.user,
      memberships: data.memberships,
      activeStudio: data.activeStudio,
    }));
  }, []);

  const logout = useCallback(async () => {
    const refreshToken = await tokenStore.getRefreshToken();
    try {
      // Best-effort: ağ hatası logout'u ENGELLEMEZ (yerel temizlik her durumda yapılır).
      await rawRequest('/api/auth/mobile/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: refreshToken ?? undefined }),
      });
    } catch {
      // yut — aşağıda tam temizlik
    }
    await tokenStore.clearAll();
    toSignedOut();
  }, [toSignedOut]);

  const value: AuthApi = {
    ...state,
    login,
    verifyMfaCode,
    verifyRecoveryCode,
    cancelChallenge,
    startMfaSetup,
    verifyMfaSetup,
    completeMfaSetup,
    selectStudio,
    logout,
    retryBootstrap: () => void bootstrap(),
  };

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth(): AuthApi {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useAuth, AuthProvider içinde kullanılmalı');
  return ctx;
}
