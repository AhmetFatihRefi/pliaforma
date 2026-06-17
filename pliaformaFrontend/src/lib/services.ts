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
};

const useMock = process.env.NEXT_PUBLIC_USE_MOCK === "true";

/** Backend hazır değilken NEXT_PUBLIC_USE_MOCK=true ile mock servise düşer. */
export const authService: AuthService = useMock ? mockAuthService : realAuthService;
