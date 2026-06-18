import { api } from "./api";
import { mockAuthService } from "./mock";
import type {
  AuthResponse,
  LoginInput,
  MeResult,
  RegisterInput,
  RegisterStudioInput,
} from "./types";

export interface AuthService {
  registerStudio(input: RegisterStudioInput): Promise<AuthResponse>;
  register(input: RegisterInput): Promise<AuthResponse>;
  login(input: LoginInput): Promise<AuthResponse>;
  switchStudio(studioId: string): Promise<AuthResponse>;
  me(): Promise<MeResult>;
  logout(): Promise<void>;
  // Sprint 16: e-posta doğrulama + şifre sıfırlama
  verifyEmail(token: string): Promise<void>;
  resendVerification(): Promise<void>;
  forgotPassword(email: string): Promise<void>;
  resetPassword(token: string, newPassword: string): Promise<void>;
}

const realAuthService: AuthService = {
  registerStudio: (input) =>
    api<AuthResponse>("/api/auth/register-studio", { method: "POST", body: input, skipAuthRetry: true }),
  register: (input) =>
    api<AuthResponse>("/api/auth/register", { method: "POST", body: input, skipAuthRetry: true }),
  login: (input) =>
    api<AuthResponse>("/api/auth/login", { method: "POST", body: input, skipAuthRetry: true }),
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
};

const useMock = process.env.NEXT_PUBLIC_USE_MOCK === "true";

/** Backend hazır değilken NEXT_PUBLIC_USE_MOCK=true ile mock servise düşer. */
export const authService: AuthService = useMock ? mockAuthService : realAuthService;
