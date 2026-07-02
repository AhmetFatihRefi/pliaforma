// Backend Auth DTO'larının birebir karşılığı. Backend sözleşmesi değişirse burası da
// aynı PR'da güncellenir (frontend↔backend senkron kuralı).

export type GlobalRole = "Standard" | "PlatformAdmin";
export type StudioRole = "Owner" | "StudioAdmin" | "Instructor" | "Student";
export type MembershipStatus = "Pending" | "Active" | "Rejected" | "Removed";
/** Sprint 17 Rev2: kayıt niyeti (UX). Stüdyosuz kullanıcının /join-studio ekranını belirler; yetki kaynağı değil. */
export type RegistrationIntent = "Unknown" | "Student" | "Instructor";

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  phone?: string | null;
  globalRole: GlobalRole;
  /** Sprint 16: e-posta doğrulandı mı (gating bu alana bakar, mesaj string'ine değil). */
  emailVerified: boolean;
  /** Sprint 17 Rev2: kayıt niyeti (öğrenci/hoca) — yalnız /join-studio UX'i için. */
  registrationIntent: RegistrationIntent;
  /** Sprint 31-MFA: TOTP MFA aktif mi. */
  mfaEnabled?: boolean;
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
  /** Sprint 31-MFA: token kısıtlı (mfa_pending); kullanıcı MFA kurmalı. */
  mfaSetupRequired?: boolean;
  /** Sprint 46: Pilot Simple People Mode global flag. */
  pilotSimplePeopleMode?: boolean;
}

/** /auth/me gövdesi. */
export interface MeResult {
  user: UserDto;
  activeStudio?: ActiveStudioDto | null;
  memberships: MembershipDto[];
  /** Sprint 31-MFA */
  mfaSetupRequired?: boolean;
  mfaRecoveryCodesRemaining?: number;
  /** Sprint 46: Pilot Simple People Mode global flag (açıksa sade öğrenci akışı UI'ı). */
  pilotSimplePeopleMode?: boolean;
}

/** Sprint 49: anonim /auth/registration-status — herkese açık stüdyo kaydı etkin mi. */
export interface RegistrationStatus {
  studioRegistrationEnabled: boolean;
}

// ---- Sprint 31-MFA ----
/** Login MFA-aktif kullanıcı için: tam token YOK; challengeToken ile TOTP gönderilir. */
export interface MfaChallengeResponse {
  mfaChallengeRequired: true;
  challengeToken: string;
}
/** login() sonucu: ya normal/setup-pending auth ya da challenge. */
export type LoginResult =
  | { kind: "auth"; auth: AuthResponse }
  | { kind: "challenge"; challengeToken: string };

export interface MfaSetupStartResponse {
  otpauthUri: string;
  manualKey: string;
}
export interface MfaSetupCompleteResponse {
  auth: AuthResponse;
  recoveryCodes: string[];
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
  /** Sprint 17 Rev2: kayıt niyeti (öğrenci/hoca). */
  intent?: RegistrationIntent;
}
export interface LoginInput {
  email: string;
  password: string;
}
