// Backend sözleşmelerinin elle yazılmış TS karşılığı (SPRINT-54-SPEC §2 — koddan doğrulandı).
// Kaynaklar: pliaformaApi Api/Auth/MobileAuthContracts.cs, Features/Auth/AuthDtos.cs,
// Features/Dashboards/DashboardDtos.cs, Features/Scheduling/ClassSessionDtos.cs.
// Sözleşme değişirse bu dosya AYNI PR'da güncellenir.

export interface ApiEnvelope<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: Record<string, string[]> | null;
}

export type StudioRole = 'Owner' | 'StudioAdmin' | 'Instructor' | 'Student';

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  phone: string | null;
  globalRole: 'Standard' | 'PlatformAdmin';
  emailVerified: boolean;
  mfaEnabled: boolean;
}

export interface ActiveStudioDto {
  membershipId: string;
  studioId: string;
  studioName: string;
  studioSlug: string;
  role: StudioRole;
}

export interface MembershipDto {
  membershipId: string;
  studioId: string;
  studioName: string;
  studioSlug: string;
  role: StudioRole;
  status: string;
}

/**
 * Mobil auth yanıtı. UYARI (K6/M3): expiry hesapları için YALNIZ `*Unix` alanları (+ JWT exp) kullanılır;
 * DateTime string alanları offset'siz İstanbul duvar saatidir ve makine-tüketimi için PARSE EDİLMEZ.
 */
export interface MobileAuthResponse {
  accessToken: string;
  refreshToken: string;
  user: UserDto;
  activeStudio: ActiveStudioDto | null;
  memberships: MembershipDto[];
  mfaSetupRequired?: boolean;
  trustedDeviceToken?: string;
  accessTokenExpiresAtUnix: number;
  refreshTokenExpiresAtUnix: number;
  trustedDeviceExpiresAtUnix?: number;
}

export interface MfaChallengeResponse {
  challengeToken: string;
  mfaChallengeRequired: true;
}

export type MobileLoginData = MobileAuthResponse | MfaChallengeResponse;

export function isMfaChallenge(d: MobileLoginData): d is MfaChallengeResponse {
  return (d as MfaChallengeResponse).mfaChallengeRequired === true;
}

export interface MfaSetupStartResponse {
  otpauthUri: string;
  manualKey: string;
}

export interface MobileMfaSetupCompleteResponse {
  auth: MobileAuthResponse;
  recoveryCodes: string[];
}

/** Web switch-studio yanıtı (refresh + Unix alanları YOKTUR; access expiry JWT exp'ten türetilir). */
export interface SwitchStudioResponse {
  accessToken: string;
  user: UserDto;
  activeStudio: ActiveStudioDto | null;
  memberships: MembershipDto[];
}

// ---- Dashboard (salt-okunur) ----
export interface AttendanceBreakdownDto {
  presentCount: number;
  absentCount: number;
  excusedCount: number;
  lateCancelCount: number;
  totalMarked: number;
  attendanceRate: number | null;
}

export interface StudioDashboardDto {
  month: string;
  totalStudents: number;
  activeStudents: number;
  guestStudents: number;
  newStudentsThisMonth: number;
  totalInstructors: number;
  sessionsThisMonth: number;
  completedOrPastSessions: number;
  cancelledSessions: number;
  bookingsThisMonth: number;
  attendance: AttendanceBreakdownDto;
  payments: {
    paidAmount: number;
    pendingAmount: number;
    overdueAmount: number;
    paidCount: number;
    pendingCount: number;
    overdueCount: number;
  };
  packages: { activePackageCount: number; usedCreditCount: number; remainingCreditTotal: number };
  makeupCredits: { totalOutstandingBalance: number; grantedThisMonth: number; usedThisMonth: number };
  upcoming: { todaySessions: number; upcomingSessionsNext7Days: number };
}

export interface ClassSessionDto {
  id: string;
  instructorName: string;
  classTypeName: string;
  /** İstanbul duvar saati string'i — yalnız GÖRÜNTÜLEME için slice edilir, Date'e parse edilmez. */
  startAt: string;
  endAt: string;
  capacity: number;
  bookedCount: number;
  status: string;
  sessionKind: string;
  isIndividual?: boolean;
}
