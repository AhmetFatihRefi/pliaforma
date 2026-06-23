import { tokenStore } from "./tokenStore";
import type {
  AuthResponse,
  LoginInput,
  MeResult,
  RegisterInput,
  RegisterStudioInput,
} from "./types";
import type { AuthService } from "./services";

// Backend hazır değilken UI'ı uçtan uca denemek için basit, durumlu mock.
// Gerçek servis ile BİREBİR imza taşır; sadece NEXT_PUBLIC_USE_MOCK=true iken devreye girer.

const delay = (ms = 400) => new Promise((r) => setTimeout(r, ms));

function fakeStudio(name: string) {
  const id = "mock-studio-" + name.toLowerCase().replace(/\s+/g, "-");
  return { id, name, slug: id };
}

function buildAuth(user: MeResult["user"], studioName: string | null): AuthResponse {
  const memberships = studioName
    ? [
        {
          membershipId: "mock-membership",
          studioId: fakeStudio(studioName).id,
          studioName,
          studioSlug: fakeStudio(studioName).slug,
          role: "Owner" as const,
          status: "Active" as const,
        },
      ]
    : [];
  const activeStudio = memberships[0]
    ? {
        membershipId: memberships[0].membershipId,
        studioId: memberships[0].studioId,
        studioName: memberships[0].studioName,
        studioSlug: memberships[0].studioSlug,
        role: memberships[0].role,
      }
    : null;

  tokenStore.setAccessToken("mock-access-token");
  if (activeStudio) tokenStore.setActiveStudioId(activeStudio.studioId);

  return {
    accessToken: "mock-access-token",
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user,
    activeStudio,
    memberships,
  };
}

let current: MeResult | null = null;

export const mockAuthService: AuthService = {
  async registerStudio(input: RegisterStudioInput) {
    await delay();
    const user = {
      id: "mock-user",
      email: input.email,
      fullName: input.ownerFullName,
      phone: input.phone,
      globalRole: "Standard" as const,
      emailVerified: true,
      registrationIntent: "Unknown" as const,
    };
    const auth = buildAuth(user, input.studioName);
    current = { user, activeStudio: auth.activeStudio, memberships: auth.memberships };
    return auth;
  },

  async register(input: RegisterInput) {
    await delay();
    const user = {
      id: "mock-user",
      email: input.email,
      fullName: input.fullName,
      phone: input.phone,
      globalRole: "Standard" as const,
      emailVerified: true,
      registrationIntent: "Unknown" as const,
    };
    const auth = buildAuth(user, null);
    current = { user, activeStudio: null, memberships: [] };
    return auth;
  },

  async login(input: LoginInput) {
    await delay();
    const user = {
      id: "mock-user",
      email: input.email,
      fullName: "Mock Kullanıcı",
      phone: null,
      globalRole: "Standard" as const,
      emailVerified: true,
      registrationIntent: "Unknown" as const,
    };
    const auth = buildAuth(user, "Zen Pilates");
    current = { user, activeStudio: auth.activeStudio, memberships: auth.memberships };
    return auth;
  },

  async switchStudio(studioId: string) {
    await delay(200);
    if (!current) throw new Error("Oturum yok");
    tokenStore.setActiveStudioId(studioId);
    return buildAuth(current.user, current.activeStudio?.studioName ?? "Zen Pilates");
  },

  async me() {
    await delay(150);
    if (!current) throw new Error("Oturum yok");
    return current;
  },

  async logout() {
    await delay(100);
    current = null;
    tokenStore.clear();
  },

  async verifyEmail() {
    await delay(100);
  },
  async resendVerification() {
    await delay(100);
  },
  async forgotPassword() {
    await delay(100);
  },
  async resetPassword() {
    await delay(100);
  },
  async changePassword() {
    await delay(100);
  },
};
