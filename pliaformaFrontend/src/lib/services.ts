import { api } from "./api";
import { mockAuthService } from "./mock";
import type {
  AuthResponse,
  LoginInput,
  LoginResult,
  MeResult,
  MfaChallengeResponse,
  MfaSetupCompleteResponse,
  MfaSetupStartResponse,
  RegisterInput,
  RegisterStudioInput,
} from "./types";

export interface AuthService {
  registerStudio(input: RegisterStudioInput): Promise<AuthResponse>;
  register(input: RegisterInput): Promise<AuthResponse>;
  login(input: LoginInput): Promise<LoginResult>;
  switchStudio(studioId: string): Promise<AuthResponse>;
  me(): Promise<MeResult>;
  logout(): Promise<void>;
  // Sprint 16: e-posta doğrulama + şifre sıfırlama
  verifyEmail(token: string): Promise<void>;
  resendVerification(): Promise<void>;
  forgotPassword(email: string): Promise<void>;
  resetPassword(token: string, newPassword: string): Promise<void>;
  // Sprint 31B-2: oturum açmış kullanıcı parolasını değiştirir
  changePassword(currentPassword: string, newPassword: string): Promise<void>;
  // Sprint 31-MFA
  mfaSetupStart(): Promise<MfaSetupStartResponse>;
  mfaSetupVerify(code: string): Promise<MfaSetupCompleteResponse>;
  mfaChallengeVerify(challengeToken: string, code: string): Promise<AuthResponse>;
  mfaRecoveryVerify(challengeToken: string, recoveryCode: string): Promise<AuthResponse>;
}

const realAuthService: AuthService = {
  registerStudio: (input) =>
    api<AuthResponse>("/api/auth/register-studio", { method: "POST", body: input, skipAuthRetry: true }),
  register: (input) =>
    api<AuthResponse>("/api/auth/register", { method: "POST", body: input, skipAuthRetry: true }),
  login: async (input) => {
    const data = await api<AuthResponse | MfaChallengeResponse>("/api/auth/login", { method: "POST", body: input, skipAuthRetry: true });
    if ("mfaChallengeRequired" in data && data.mfaChallengeRequired)
      return { kind: "challenge", challengeToken: data.challengeToken };
    return { kind: "auth", auth: data as AuthResponse };
  },
  switchStudio: (studioId) =>
    api<AuthResponse>("/api/auth/switch-studio", { method: "POST", body: { studioId } }),
  me: () => api<MeResult>("/api/auth/me"),
  logout: () => api<null>("/api/auth/logout", { method: "POST", skipAuthRetry: true }).then(() => undefined),
  verifyEmail: (token) =>
    api<null>("/api/auth/verify-email", { method: "POST", body: { token }, skipAuthRetry: true }).then(() => undefined),
  resendVerification: () =>
    api<null>("/api/auth/resend-verification-email", { method: "POST", body: {} }).then(() => undefined),
  forgotPassword: (email) =>
    api<null>("/api/auth/forgot-password", { method: "POST", body: { email }, skipAuthRetry: true }).then(() => undefined),
  resetPassword: (token, newPassword) =>
    api<null>("/api/auth/reset-password", { method: "POST", body: { token, newPassword }, skipAuthRetry: true }).then(() => undefined),
  changePassword: (currentPassword, newPassword) =>
    api<null>("/api/auth/change-password", { method: "POST", body: { currentPassword, newPassword }, skipAuthRetry: true }).then(() => undefined),
  mfaSetupStart: () =>
    api<MfaSetupStartResponse>("/api/auth/mfa/setup/start", { method: "POST" }),
  mfaSetupVerify: (code) =>
    api<MfaSetupCompleteResponse>("/api/auth/mfa/setup/verify", { method: "POST", body: { code } }),
  mfaChallengeVerify: (challengeToken, code) =>
    api<AuthResponse>("/api/auth/mfa/challenge/verify", { method: "POST", body: { challengeToken, code }, skipAuthRetry: true }),
  mfaRecoveryVerify: (challengeToken, recoveryCode) =>
    api<AuthResponse>("/api/auth/mfa/recovery-code/verify", { method: "POST", body: { challengeToken, recoveryCode }, skipAuthRetry: true }),
};

const useMock = process.env.NEXT_PUBLIC_USE_MOCK === "true";

/** Backend hazır değilken NEXT_PUBLIC_USE_MOCK=true ile mock servise düşer. */
export const authService: AuthService = useMock ? mockAuthService : realAuthService;
