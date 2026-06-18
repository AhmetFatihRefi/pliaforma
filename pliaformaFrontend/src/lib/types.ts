// Backend Auth DTO'larının birebir karşılığı. Backend sözleşmesi değişirse burası da
// aynı PR'da güncellenir (frontend↔backend senkron kuralı).

export type GlobalRole = "Standard" | "PlatformAdmin";
export type StudioRole = "Owner" | "StudioAdmin" | "Instructor" | "Student";
export type MembershipStatus = "Pending" | "Active" | "Rejected" | "Removed";

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  phone?: string | null;
  globalRole: GlobalRole;
  /** Sprint 16: e-posta doğrulandı mı (gating bu alana bakar, mesaj string'ine değil). */
  emailVerified: boolean;
}

export interface MembershipDto {
  membershipId: string;
  studioId: string;
  studioName: string;
  studioSlug: string;
  role: StudioRole;
  status: MembershipStatus;
}

export interface ActiveStudioDto {
  membershipId: string;
  studioId: string;
  studioName: string;
  studioSlug: string;
  role: StudioRole;
}

/** /auth/login, /auth/register*, /auth/refresh, /auth/switch-studio gövdesi (refresh token YOK). */
export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserDto;
  activeStudio?: ActiveStudioDto | null;
  memberships: MembershipDto[];
}

/** /auth/me gövdesi. */
export interface MeResult {
  user: UserDto;
  activeStudio?: ActiveStudioDto | null;
  memberships: MembershipDto[];
}

/** Tüm yanıtların standart zarfı. */
export interface ApiEnvelope<T> {
  success: boolean;
  message?: string | null;
  data?: T | null;
  errors?: Record<string, string[]> | null;
}

// İstek gövdeleri
export interface RegisterStudioInput {
  studioName: string;
  ownerFullName: string;
  email: string;
  password: string;
  phone?: string;
}
export interface RegisterInput {
  fullName: string;
  email: string;
  password: string;
  phone?: string;
}
export interface LoginInput {
  email: string;
  password: string;
}
