"use client";

import { createContext, useContext, useEffect, useState } from "react";
import { authService } from "./services";
import { tokenStore } from "./tokenStore";
import type {
  ActiveStudioDto,
  AuthResponse,
  LoginInput,
  LoginResult,
  MembershipDto,
  RegisterInput,
  RegisterStudioInput,
  UserDto,
} from "./types";

interface AuthState {
  user: UserDto | null;
  activeStudio: ActiveStudioDto | null;
  memberships: MembershipDto[];
  loading: boolean;
  /** Sprint 31-MFA: token kısıtlı (mfa_pending) → kullanıcı MFA kurmalı (dashboard gate). */
  mfaSetupRequired: boolean;
}

interface AuthContextValue extends AuthState {
  login: (input: LoginInput) => Promise<LoginResult>;
  registerStudio: (input: RegisterStudioInput) => Promise<AuthResponse>;
  register: (input: RegisterInput) => Promise<AuthResponse>;
  switchStudio: (studioId: string) => Promise<AuthResponse>;
  logout: () => Promise<void>;
  /** Sprint 16: /me'yi yeniden çekip durumu (örn. emailVerified) günceller. */
  reloadMe: () => Promise<void>;
  /** Sprint 31-MFA: MFA challenge/setup sonrası dönen auth'u uygular. */
  applyAuth: (auth: AuthResponse) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    user: null,
    activeStudio: null,
    memberships: [],
    loading: true,
    mfaSetupRequired: false,
  });

  const applyAuth = (auth: AuthResponse) => {
    tokenStore.setAccessToken(auth.accessToken);
    tokenStore.setActiveStudioId(auth.activeStudio?.studioId ?? null);
    setState({
      user: auth.user,
      activeStudio: auth.activeStudio ?? null,
      memberships: auth.memberships,
      loading: false,
      mfaSetupRequired: auth.mfaSetupRequired ?? false,
    });
  };

  // İlk yüklemede oturumu geri yükle: refresh cookie varsa /me 401→refresh→retry ile çalışır.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const me = await authService.me();
        if (!cancelled)
          setState({
            user: me.user,
            activeStudio: me.activeStudio ?? null,
            memberships: me.memberships,
            loading: false,
            mfaSetupRequired: me.mfaSetupRequired ?? false,
          });
      } catch {
        if (!cancelled) setState({ user: null, activeStudio: null, memberships: [], loading: false, mfaSetupRequired: false });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const login = async (input: LoginInput) => {
    const result = await authService.login(input);
    if (result.kind === "auth") applyAuth(result.auth);
    return result;
  };

  const registerStudio = async (input: RegisterStudioInput) => {
    const auth = await authService.registerStudio(input);
    applyAuth(auth);
    return auth;
  };

  const register = async (input: RegisterInput) => {
    const auth = await authService.register(input);
    applyAuth(auth);
    return auth;
  };

  const switchStudio = async (studioId: string) => {
    const auth = await authService.switchStudio(studioId);
    applyAuth(auth);
    return auth;
  };

  const logout = async () => {
    try {
      await authService.logout();
    } finally {
      tokenStore.clear();
      setState({ user: null, activeStudio: null, memberships: [], loading: false, mfaSetupRequired: false });
    }
  };

  const reloadMe = async () => {
    try {
      const me = await authService.me();
      setState({ user: me.user, activeStudio: me.activeStudio ?? null, memberships: me.memberships, loading: false, mfaSetupRequired: me.mfaSetupRequired ?? false });
    } catch {
      /* oturum yoksa sessiz geç */
    }
  };

  return (
    <AuthContext.Provider value={{ ...state, login, registerStudio, register, switchStudio, logout, reloadMe, applyAuth }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth, AuthProvider içinde kullanılmalı.");
  return ctx;
}
