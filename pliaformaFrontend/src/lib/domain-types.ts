// Sprint 1 backend DTO'larının birebir karşılığı (senkron sözleşme).
import type { StudioRole, MembershipStatus } from "./types";

export type EquipmentType = "Mat" | "Reformer" | "Cadillac" | "Chair" | "Barrel";
export type ClassCapacityType = "Private" | "Duo" | "Group";
export type ClassLevel = "Beginner" | "Intermediate" | "Advanced" | "AllLevels";
export type ClassPurpose = "General" | "ClinicalRehab" | "Prenatal";
export type RateType = "PerSession" | "PerHour" | "PerStudentPerSession";
export type MembershipDirection = "InvitedByStudio" | "RequestedByUser";

export interface ClassTypeDto {
  id: string;
  name: string;
  equipment: EquipmentType;
  capacityType: ClassCapacityType;
  maxCapacity: number;
  level: ClassLevel;
  purpose: ClassPurpose;
  defaultDurationMinutes: number;
  description?: string | null;
  isActive: boolean;
}

export type ClassTypeInput = Omit<ClassTypeDto, "id"> & { isActive?: boolean };

export interface MemberDto {
  membershipId: string;
  userId: string;
  fullName: string;
  email: string;
  role: StudioRole;
  status: MembershipStatus;
  direction: MembershipDirection;
  instructorProfileId?: string | null;
  rate?: number | null;
  rateType?: RateType | null;
}

export interface StudioDto {
  id: string;
  name: string;
  slug: string;
  timeZoneId: string;
  currency: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  addressLine?: string | null;
  city?: string | null;
  isActive: boolean;
}

export interface InstructorAssignmentDto {
  id: string;
  classTypeId: string;
  classTypeName: string;
  rateOverride?: number | null;
}

export interface PendingInvitationDto {
  membershipId: string;
  studioId: string;
  studioName: string;
  studioSlug: string;
  role: StudioRole;
  invitedAt: string;
}

// Sprint 15: self-service kayıt & katılım isteği
export interface JoinRequestDto {
  membershipId: string;
  applicantName: string;
  applicantEmail: string;
  requestedRole: StudioRole;
  requestedAt: string;
  direction: MembershipDirection;
  status: MembershipStatus;
}

export interface MyJoinRequestDto {
  membershipId: string;
  studioId: string;
  studioName: string;
  requestedRole: StudioRole;
  status: MembershipStatus;
  requestedAt: string;
  rejectedAt?: string | null;
  message?: string | null;
}

export interface JoinCodeDto {
  joinCode: string;
  formattedJoinCode: string;
  isActive: boolean;
  updatedAt?: string | null;
}

// Etiketler (UI)
export const equipmentLabels: Record<EquipmentType, string> = {
  Mat: "Mat", Reformer: "Reformer", Cadillac: "Cadillac", Chair: "Sandalye", Barrel: "Barrel",
};
export const capacityLabels: Record<ClassCapacityType, string> = {
  Private: "Birebir", Duo: "İkili", Group: "Grup",
};
export const levelLabels: Record<ClassLevel, string> = {
  Beginner: "Başlangıç", Intermediate: "Orta", Advanced: "İleri", AllLevels: "Tüm seviyeler",
};
export const purposeLabels: Record<ClassPurpose, string> = {
  General: "Genel", ClinicalRehab: "Klinik/Rehab", Prenatal: "Prenatal",
};
export const rateTypeLabels: Record<RateType, string> = {
  PerSession: "Seans başına", PerHour: "Saat başına", PerStudentPerSession: "Öğrenci/seans",
};

// ===== Sprint 2: Öğrenciler / Misafir akışı =====
export type StudentStatus = "Guest" | "Active" | "Inactive";
export type StudentSource = "Phone" | "WalkIn" | "Web" | "Referral" | "Other";

export interface StudentDto {
  id: string;
  userId?: string | null;
  studioMembershipId?: string | null;
  fullName: string;
  phone?: string | null;
  email?: string | null;
  status: StudentStatus;
  source: StudentSource;
  notes?: string | null;
  healthNotes?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  birthDate?: string | null;
  createdByUserId: string;
  convertedAt?: string | null;
  isActive: boolean;
}

export interface GuestInput {
  fullName: string;
  phone?: string;
  email?: string;
  source: StudentSource;
  notes?: string;
  healthNotes?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  birthDate?: string | null;
}

export const studentStatusLabels: Record<StudentStatus, string> = {
  Guest: "Misafir", Active: "Aktif", Inactive: "Pasif",
};
export const studentSourceLabels: Record<StudentSource, string> = {
  Phone: "Telefon", WalkIn: "Kapıdan", Web: "Web", Referral: "Tavsiye", Other: "Diğer",
};

// ===== Sprint 3: Müsaitlik & Ders Seansları =====
export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";
export type ClassSessionStatus = "Scheduled" | "Cancelled" | "Completed";

export interface InstructorAvailabilityDto {
  id: string;
  instructorProfileId: string;
  dayOfWeek: DayOfWeek;
  startTime: string; // "HH:mm:ss"
  endTime: string;
  isActive: boolean;
}

export interface ClassSessionDto {
  id: string;
  instructorProfileId: string;
  instructorName: string;
  classTypeId: string;
  classTypeName: string;
  startAt: string; // ISO (studio-yerel duvar saati)
  endAt: string;
  capacity: number;
  bookedCount: number;
  remainingCapacity: number;
  isFull: boolean;
  status: ClassSessionStatus;
  sessionKind: ClassSessionKind;
  notes?: string | null;
  createdByUserId: string;
  rescheduledAt?: string | null;
  /** Sprint 18a: bireysel ders mi (grup değil). */
  isIndividual?: boolean;
  /** Sprint 18a: fiilî hoca (substitution; mantık Faz 2). */
  actualInstructorProfileId?: string | null;
  /** Sprint 18a: planlanan ≠ fiilî hoca. */
  instructorChanged?: boolean;
}

export interface BulkGenerateResult {
  created: ClassSessionDto[];
  skipped: { startAt: string; reason: string }[];
}

export interface InstructorSelfDto {
  instructorProfileId: string;
  displayName: string;
  rate: number;
  rateType: RateType;
}

// System.DayOfWeek sırası: Sunday=0 .. Saturday=6
export const daysOfWeek: DayOfWeek[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
export const dayLabels: Record<DayOfWeek, string> = {
  Monday: "Pazartesi", Tuesday: "Salı", Wednesday: "Çarşamba", Thursday: "Perşembe",
  Friday: "Cuma", Saturday: "Cumartesi", Sunday: "Pazar",
};
export const sessionStatusLabels: Record<ClassSessionStatus, string> = {
  Scheduled: "Planlı", Cancelled: "İptal", Completed: "Tamamlandı",
};

/**
 * Sprint 18a: grup seansları backend'de "sınırsız kapasite" için yüksek bir sentinel (9999) ile temsil edilir.
 * Bu teknik değer kullanıcıya GÖSTERİLMEZ — UI bu eşik ve üzerini "limitsiz" sayar.
 */
export const UNLIMITED_SESSION_CAPACITY = 9999;
export const isUnlimitedCapacity = (capacity: number) => capacity >= UNLIMITED_SESSION_CAPACITY;

// ===== Sprint 18a: Öğrenci sözleşmesi (sözleşme-tabanlı program) =====
export type StudentContractStatus = "Active" | "Completed" | "Cancelled" | "Expired";
export const studentContractStatusLabels: Record<StudentContractStatus, string> = {
  Active: "Aktif", Completed: "Tamamlandı", Cancelled: "İptal", Expired: "Süresi doldu",
};
export interface StudentContractDto {
  id: string;
  studentProfileId: string;
  studentName: string;
  classTypeId: string;
  classTypeName: string;
  instructorProfileId: string;
  instructorName: string;
  startDate: string;
  endDate: string;
  totalLessonCount: number;
  makeupAllowance: number;
  price: number;
  currency: string;
  isIndividualDefault: boolean;
  status: StudentContractStatus;
  notes?: string | null;
  scheduledLessonCount: number;
  availableLessonCount: number;
}
export interface ContractScheduleSlotInput { dayOfWeek: DayOfWeek; startTime: string }
export interface ContractScheduleSlotPreviewDto { startAt: string; endAt: string; plannable: boolean; skipReason?: string | null }
export interface ContractSchedulePreviewDto { availableLessonCount: number; plannableCount: number; slots: ContractScheduleSlotPreviewDto[] }
export interface GenerateContractScheduleResultDto { createdCount: number; newSessionCount: number; reusedSessionCount: number; skippedCount: number; createdStartAts: string[] }

// ===== Sprint 4: Rezervasyon =====
export type ClassBookingStatus = "Booked" | "CancelledByStudent" | "CancelledByStudio";

/** Sprint 14: paket uygunluk değerlendirmesi. */
export type PackageEligibilityStatus =
  | "Eligible" | "NoPackageAtAll" | "NoBalance" | "WrongClassType" | "RequiresPayment" | "Resolved";

export const packageEligibilityLabels: Record<PackageEligibilityStatus, string> = {
  Eligible: "Uygun",
  NoPackageAtAll: "Yeni paket almanız gerekiyor",
  NoBalance: "Paket hakkınız bitmiş",
  WrongClassType: "Paketiniz bu ders için geçerli değil",
  RequiresPayment: "Ödeme gerekli (paketsiz rezervasyon)",
  Resolved: "Çözüldü (pakete bağlandı)",
};

export interface ClassBookingDto {
  id: string;
  classSessionId: string;
  studentProfileId: string;
  studentName: string;
  status: ClassBookingStatus;
  startAt: string;
  endAt: string;
  classTypeName: string;
  instructorName: string;
  cancelledAt?: string | null;
  /** Sprint 14: paketsiz/yetersiz hakla (staff) yapıldıysa ödeme gerekir. */
  requiresPayment?: boolean;
  packageEligibilityStatus?: PackageEligibilityStatus | null;
  expectedStudentPackageId?: string | null;
}

export interface MyBookingDto {
  bookingId: string;
  classSessionId: string;
  classTypeName: string;
  instructorName: string;
  startAt: string;
  endAt: string;
  status: ClassBookingStatus;
}

export interface StudentSessionDto {
  id: string;
  classTypeName: string;
  instructorName: string;
  startAt: string;
  endAt: string;
  capacity: number;
  bookedCount: number;
  remainingCapacity: number;
  isFull: boolean;
  sessionKind: ClassSessionKind;
  requiresMakeupCredit: boolean;
  bookingId?: string | null;
  alreadyBooked: boolean;
  canBook: boolean;
  /** Sprint 14: paket uygunluğu (Regular seans). Makeup/Trial/Special → her zaman Eligible. */
  packageEligibility: PackageEligibilityStatus;
  packageEligibilityMessage?: string | null;
  expectedPackageName?: string | null;
  availableCredit: number;
  packageEligible: boolean;
}

export const bookingStatusLabels: Record<ClassBookingStatus, string> = {
  Booked: "Rezerve", CancelledByStudent: "İptal (öğrenci)", CancelledByStudio: "İptal (stüdyo)",
};

// ===== Sprint 5: Erteleme, İptal, Telafi Kredileri =====
export type ClassSessionKind = "Regular" | "Makeup" | "Trial" | "Special";

export const sessionKindLabels: Record<ClassSessionKind, string> = {
  Regular: "Normal", Makeup: "Telafi", Trial: "Deneme", Special: "Özel",
};

export type MakeupCreditTransactionType = "Granted" | "Used" | "Restored" | "Revoked" | "Expired";
export type MakeupCreditReason =
  | "StudioCancelledClass" | "HolidayClosure" | "ManualByOwner" | "ManualByInstructor"
  | "MakeupBookingUsed" | "MakeupBookingCancelled" | "StudentCancelledInTime" | "NoShowApproved"
  | "Other" | "ExcusedAbsence";

export const creditTypeLabels: Record<MakeupCreditTransactionType, string> = {
  Granted: "Verildi", Used: "Kullanıldı", Restored: "İade", Revoked: "Geri alındı", Expired: "Süre doldu",
};
export const creditReasonLabels: Record<MakeupCreditReason, string> = {
  StudioCancelledClass: "Stüdyo dersi iptal etti",
  HolidayClosure: "Tatil/kapanış",
  ManualByOwner: "Manuel (yönetim)",
  ManualByInstructor: "Manuel (hoca)",
  MakeupBookingUsed: "Telafi rezervasyonu",
  MakeupBookingCancelled: "Telafi iptali iadesi",
  StudentCancelledInTime: "Zamanında iptal",
  NoShowApproved: "Geç iptal (onaylı)",
  Other: "Diğer",
  ExcusedAbsence: "Mazeretli devamsızlık",
};

export interface MakeupCreditTransactionDto {
  id: string;
  amount: number;
  type: MakeupCreditTransactionType;
  reason: MakeupCreditReason;
  sourceClassSessionId?: string | null;
  sourceBookingId?: string | null;
  usedForBookingId?: string | null;
  expiresAt?: string | null;
  notes?: string | null;
  createdAt: string;
  // Sprint 20: kaynak ders + kullanıldığı telafi dersi etiketleri.
  sourceLabel?: string | null;
  usedForLabel?: string | null;
}

// Sprint 20: kazanılan / kullanılan / kullanılabilir özeti.
export interface MakeupCreditSummaryDto {
  earned: number;
  used: number;
  available: number;
}

export interface StudentMakeupCreditsDto {
  studentProfileId: string;
  studentName: string;
  balance: number;
  summary: MakeupCreditSummaryDto;
  transactions: MakeupCreditTransactionDto[];
}

export interface MyMakeupCreditsDto {
  balance: number;
  summary: MakeupCreditSummaryDto;
  transactions: MakeupCreditTransactionDto[];
}

// Sprint 20: telafi dersine eklenebilir öğrenci (kullanılabilir hak > 0).
export interface MakeupEligibleStudentDto {
  studentProfileId: string;
  studentName: string;
  availableCredits: number;
}

// Sprint 21: zincirleme kaydırma önizleme/sonuç.
export interface ChainShiftItemDto {
  sessionId: string;
  classTypeName: string;
  oldStartAt: string;
  oldEndAt: string;
  newStartAt: string;
  newEndAt: string;
  conflictReason?: string | null; // "past" | "instructor" | "student"
  outsideAvailability: boolean;
}
export interface ChainShiftPreviewDto {
  affected: ChainShiftItemDto[];
  canApply: boolean;
}

// Sprint 22: köklü grup düzeni değişikliği.
export interface GroupChangeCandidateDto {
  contractId: string;
  studentProfileId: string;
  studentName: string;
  availableLessonCount: number;
  startDate: string;
  endDate: string;
  futureRegularCount: number;
  currentSlotSummary: string;
  isIndividual: boolean;
}
export interface GroupChangeSlotOutcomeDto {
  startAt: string;
  endAt: string;
  outcome: string; // created-new | reused | skipped | conflict
  reason?: string | null;
}
export interface GroupChangeStudentResultDto {
  contractId: string;
  studentName: string;
  removedCount: number;
  plannedCount: number;
  reusedCount: number;
  newSessionCount: number;
  skippedCount: number;
  conflictCount: number;
  note?: string | null;
  slots: GroupChangeSlotOutcomeDto[];
}
export interface GroupChangePreviewDto {
  students: GroupChangeStudentResultDto[];
  emptiedSessionsCancelled: number;
  canApply: boolean;
}

export interface MakeupPolicyDto {
  monthlyManualCreditLimit: number;
  defaultCreditValidityDays: number;
  allowInstructorManualGrant: boolean;
  allowMakeupBookingWithoutCreditByAdmin: boolean;
}

// Toplu kapanış / erteleme
export type StudioClosureActionType = "CancelSessions" | "CancelAndGrantMakeupCredit" | "RescheduleByOffset";

export const closureActionLabels: Record<StudioClosureActionType, string> = {
  CancelSessions: "Seansları iptal et",
  CancelAndGrantMakeupCredit: "İptal et + telafi kredisi ver",
  RescheduleByOffset: "Gün ofsetiyle ertele",
};

export interface StudioClosureDto {
  id: string;
  title: string;
  reason?: string | null;
  startAt: string;
  endAt: string;
  actionType: StudioClosureActionType;
  offsetDays?: number | null;
  affectedInstructorProfileId?: string | null;
  affectedClassTypeId?: string | null;
  affectedCount: number;
  cancelledCount: number;
  creditedCount: number;
  rescheduledCount: number;
  skippedCount: number;
  createdAt: string;
}

export interface ClosureSessionOutcomeDto {
  sessionId: string;
  originalStartAt: string;
  newStartAt?: string | null;
  outcome: string;
  reason?: string | null;
}

export interface CreateClosureResult {
  closure: StudioClosureDto;
  sessions: ClosureSessionOutcomeDto[];
}

// ===== Sprint 6: Yoklama / Katılım =====
export type AttendanceStatus = "Present" | "Absent" | "Excused" | "LateCancel";

export const attendanceStatusLabels: Record<AttendanceStatus, string> = {
  Present: "Geldi", Absent: "Gelmedi", Excused: "Mazeretli", LateCancel: "Geç İptal",
};

// Sprint 19: yoklamadan paket tüketimi kaldırıldı (Regular = sözleşme).
export interface AttendanceItemDto {
  bookingId: string;
  studentProfileId: string;
  studentName: string;
  status: AttendanceStatus | null;
  notes?: string | null;
  creditGranted: boolean;
  markedAt?: string | null;
}

export interface SessionAttendanceDto {
  sessionId: string;
  classTypeName: string;
  instructorName: string;
  startAt: string;
  endAt: string;
  status: ClassSessionStatus;
  canMark: boolean;
  items: AttendanceItemDto[];
}

export interface AttendanceMarkInput {
  bookingId: string;
  status: AttendanceStatus;
  notes?: string;
  grantMakeupCredit?: boolean;
}

/** Sprint 19: "Bugünü onayla" sonucu. */
export interface ConfirmTodayResultDto {
  confirmedCount: number;
  presentMarkedCount: number;
  skippedFutureCount: number;
  alreadyCompletedCount: number;
}

export interface MyAttendanceDto {
  id: string;
  classSessionId: string;
  classTypeName: string;
  instructorName: string;
  startAt: string;
  endAt: string;
  status: AttendanceStatus;
  creditGranted: boolean;
  notes?: string | null;
}

export interface AttendanceSummaryDto {
  month: string;
  presentCount: number;
  absentCount: number;
  excusedCount: number;
  lateCancelCount: number;
  totalMarked: number;
  attendanceRate?: number | null;
}

// ===== Sprint 7: Paketler ve Ödemeler =====
export interface PackagePlanDto {
  id: string;
  name: string;
  description?: string | null;
  creditCount: number;
  price: number;
  validityDays?: number | null;
  isActive: boolean;
  // Sprint 14: ders grubu kapsamı + fiyat versiyonlama
  allowAllClassTypes: boolean;
  classTypeIds: string[];
  currentEffectivePrice: number;
  currentEffectivePriceCurrency: string;
  nextEffectivePrice?: number | null;
  nextEffectiveFrom?: string | null;
}

export interface PackagePlanInput {
  name: string;
  description?: string | null;
  creditCount: number;
  price: number;
  validityDays?: number | null;
  isActive?: boolean;
  allowAllClassTypes: boolean;
  classTypeIds: string[];
  /** Opsiyonel gelecek fiyat (Create: Future*, Update: New*). */
  futurePrice?: number | null;
  futureEffectiveFrom?: string | null;
}

export interface PackagePlanPriceVersionDto {
  id: string;
  price: number;
  currency: string;
  effectiveFrom: string;
  isCurrent: boolean;
  createdAt: string;
}

export type StudentPackageStatus = "Active" | "UsedUp" | "Expired" | "Cancelled";
export const packageStatusLabels: Record<StudentPackageStatus, string> = {
  Active: "Aktif", UsedUp: "Tükendi", Expired: "Süre doldu", Cancelled: "İptal",
};

export interface StudentPackageDto {
  id: string;
  studentProfileId: string;
  studentName: string;
  packagePlanId?: string | null;
  name: string;
  initialCreditCount: number;
  currentCreditBalance: number;
  price: number;
  validFrom: string;
  validUntil?: string | null;
  status: StudentPackageStatus;
  /** DB status Active ama ValidUntil geçmişse Expired döner (dinamik; bkz. packages-and-payments.md). */
  effectiveStatus: StudentPackageStatus;
  createdAt: string;
  /** Sprint 14: satın alma anında snapshot'lanan ders grubu kapsamı. */
  allowAllClassTypesSnapshot: boolean;
  allowedClassTypeIds: string[];
}

export type PackageTxType = "Granted" | "Used" | "Restored" | "Adjusted" | "Revoked";
export type PackageTxReason =
  | "PackagePurchased" | "AttendancePresent" | "AttendanceAbsent" | "AttendanceLateCancel"
  | "ManualAdjustment" | "AdminCorrection" | "BookingCancelled" | "Other";

export const packageTxTypeLabels: Record<PackageTxType, string> = {
  Granted: "Tanımlandı", Used: "Kullanıldı", Restored: "İade", Adjusted: "Düzeltme", Revoked: "Geri alındı",
};
export const packageTxReasonLabels: Record<PackageTxReason, string> = {
  PackagePurchased: "Paket satın alındı",
  AttendancePresent: "Yoklama (geldi)",
  AttendanceAbsent: "Yoklama (gelmedi)",
  AttendanceLateCancel: "Yoklama (geç iptal)",
  ManualAdjustment: "Manuel düzeltme",
  AdminCorrection: "Yönetici düzeltmesi",
  BookingCancelled: "Rezervasyon iptali",
  Other: "Diğer",
};

export interface StudentPackageTransactionDto {
  id: string;
  amount: number;
  type: PackageTxType;
  reason: PackageTxReason;
  sourceAttendanceId?: string | null;
  sourceClassBookingId?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface StudentPackageDetailDto {
  package: StudentPackageDto;
  transactions: StudentPackageTransactionDto[];
}

export type PaymentStatus = "Pending" | "Paid" | "Overdue" | "Cancelled" | "Refunded";
export type PaymentMethod = "Cash" | "Card" | "BankTransfer" | "Other";

export const paymentStatusLabels: Record<PaymentStatus, string> = {
  Pending: "Bekliyor", Paid: "Ödendi", Overdue: "Gecikti", Cancelled: "İptal", Refunded: "İade",
};
export const paymentMethodLabels: Record<PaymentMethod, string> = {
  Cash: "Nakit", Card: "Kart", BankTransfer: "Havale/EFT", Other: "Diğer",
};

export interface PaymentDto {
  id: string;
  studentProfileId: string;
  studentName: string;
  studentPackageId?: string | null;
  amount: number;
  currency: string;
  dueDate: string;
  paidAt?: string | null;
  status: PaymentStatus;
  method?: PaymentMethod | null;
  notes?: string | null;
  createdAt: string;
}

// ===== Sprint 8: Dashboard & Raporlar =====
export interface AttendanceBreakdownDto {
  presentCount: number;
  absentCount: number;
  excusedCount: number;
  lateCancelCount: number;
  totalMarked: number;
  attendanceRate?: number | null;
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
  payments: { paidAmount: number; pendingAmount: number; overdueAmount: number; paidCount: number; pendingCount: number; overdueCount: number };
  packages: { activePackageCount: number; usedCreditCount: number; remainingCreditTotal: number };
  makeupCredits: { totalOutstandingBalance: number; grantedThisMonth: number; usedThisMonth: number };
  upcoming: { todaySessions: number; upcomingSessionsNext7Days: number };
}

export interface ClassTypeCountDto { classTypeId: string; classTypeName: string; sessionCount: number }

export interface InstructorDashboardDto {
  month: string;
  instructorProfileId: string;
  instructorName: string;
  sessionsThisMonth: number;
  cancelledSessions: number;
  bookingsCount: number;
  presentCount: number;
  absentCount: number;
  excusedCount: number;
  totalStudentsTaught: number;
  classTypeBreakdown: ClassTypeCountDto[];
}

export interface StudentMiniBookingDto { bookingId: string; classSessionId: string; classTypeName: string; instructorName: string; startAt: string; endAt: string }
export interface StudentMiniAttendanceDto { id: string; classTypeName: string; startAt: string; status: AttendanceStatus }
export interface StudentMiniMakeupDto { id: string; amount: number; type: string; reason: string; createdAt: string }

export interface StudentDashboardDto {
  month: string;
  studentProfileId: string;
  studentName: string;
  status: string;
  upcomingBookings: StudentMiniBookingDto[];
  attendanceSummary: AttendanceBreakdownDto;
  packageSummary: { activePackages: number; totalRemainingCredits: number; nearestPackageExpiry?: string | null };
  paymentSummary?: { pendingAmount: number; overdueAmount: number; nextDueDate?: string | null } | null;
  makeupCreditBalance: number;
  recentMakeupTransactions: StudentMiniMakeupDto[];
  recentAttendance: StudentMiniAttendanceDto[];
  recentBookings: StudentMiniBookingDto[];
}

// Sprint 19: parasız & operasyonel; atıf fiilî hocaya.
export interface InstructorReportRowDto {
  instructorProfileId: string;
  instructorName: string;
  sessions: number;
  groupSessions: number;
  individualSessions: number;
  distinctStudents: number;
  substitutionSessions: number;
  classTypeBreakdown: ClassTypeCountDto[];
}

export interface PaymentsReportDto {
  month: string;
  totals: { paidAmount: number; pendingAmount: number; overdueAmount: number; paidCount: number; pendingCount: number; overdueCount: number };
  payments: PaymentDto[];
}

export interface InstructorsReportDto { period: string; instructors: InstructorReportRowDto[] }

// ===== Sprint 9: Bildirimler =====
export type NotificationType =
  | "ClassReminder" | "PaymentDueSoon" | "PaymentOverdue"
  | "MakeupCreditGranted" | "BookingCancelled" | "SessionRescheduled" | "System"
  | "MembershipInvite";
export type NotificationChannel = "InApp" | "Log" | "Email" | "Sms" | "WhatsApp";
export type NotificationStatus = "Pending" | "Sent" | "Failed" | "Read";

export const notificationTypeLabels: Record<NotificationType, string> = {
  ClassReminder: "Ders hatırlatma",
  PaymentDueSoon: "Ödeme yaklaşıyor",
  PaymentOverdue: "Ödeme gecikti",
  MakeupCreditGranted: "Telafi kredisi",
  BookingCancelled: "Rezervasyon iptali",
  SessionRescheduled: "Ders ertelendi",
  System: "Sistem",
  MembershipInvite: "Stüdyo daveti",
};

export interface NotificationDto {
  id: string;
  type: NotificationType;
  channel: NotificationChannel;
  status: NotificationStatus;
  title: string;
  body: string;
  relatedClassSessionId?: string | null;
  relatedClassBookingId?: string | null;
  relatedPaymentId?: string | null;
  sentAtUtc?: string | null;
  readAtUtc?: string | null;
  createdAt: string;
  isRead: boolean;
}

export interface ReminderRunResultDto {
  classReminders: number;
  paymentDueSoon: number;
  paymentOverdue: number;
  total: number;
}

// ===== Sprint 10: Platform Admin & Abonelik & Gözlemlenebilirlik =====
export type StudioStatus = "Active" | "Suspended" | "Cancelled";
export type SubscriptionStatus = "Trialing" | "Active" | "PastDue" | "Suspended" | "Cancelled";
export type BillingPeriod = "Monthly" | "Yearly";

export const studioStatusLabels: Record<StudioStatus, string> = {
  Active: "Aktif",
  Suspended: "Askıda",
  Cancelled: "İptal",
};

export const subscriptionStatusLabels: Record<SubscriptionStatus, string> = {
  Trialing: "Deneme",
  Active: "Aktif",
  PastDue: "Gecikmiş",
  Suspended: "Askıda",
  Cancelled: "İptal",
};

export const billingPeriodLabels: Record<BillingPeriod, string> = {
  Monthly: "Aylık",
  Yearly: "Yıllık",
};

export interface PlatformOverviewDto {
  totalStudios: number;
  activeStudios: number;
  suspendedStudios: number;
  cancelledStudios: number;
  totalUsers: number;
  totalStudents: number;
  totalInstructors: number;
  totalBookingsThisMonth: number;
  totalPaymentsThisMonth: number;
  overduePaymentAmount: number;
  notificationsPending: number;
  notificationsFailed: number;
  lastNotificationActivityUtc?: string | null;
  databaseHealthy: boolean;
  missingSubscriptionCount: number;
  manualSubscriptionRevenueThisMonth: number;
  periodLabel: string;
}

export interface PlatformStudioListItemDto {
  id: string;
  name: string;
  slug: string;
  status: StudioStatus;
  ownerName: string;
  ownerEmail: string;
  userCount: number;
  studentCount: number;
  instructorCount: number;
  bookingCountThisMonth: number;
  paymentAmountThisMonth: number;
  subscriptionStatus?: SubscriptionStatus | null;
  planName?: string | null;
  createdAt: string;
}

export interface PlatformOwnerDto {
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
}

// ===== Sprint 13: Abonelik yaşam döngüsü, plan kataloğu, lisans =====
export type EffectiveSubscriptionStatus =
  | "Trialing" | "Active" | "PastDueGrace" | "CancelPending" | "Expired" | "FailOpenLicensed";

export const effectiveStatusLabels: Record<EffectiveSubscriptionStatus, string> = {
  Trialing: "Deneme",
  Active: "Aktif",
  PastDueGrace: "Ödeme bekleniyor",
  CancelPending: "Dönem sonunda iptal",
  Expired: "Süresi doldu",
  FailOpenLicensed: "Abonelik yok (geçici)",
};

export interface PlatformSubscriptionDto {
  id: string;
  studioId: string;
  subscriptionPlanId?: string | null;
  planName: string;
  tier?: string | null;
  status: SubscriptionStatus;
  monthlyPrice: number;
  introMonthlyPrice?: number | null;
  introPeriodsRemaining?: number | null;
  currency: string;
  billingPeriod: BillingPeriod;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEndsAt?: string | null;
  cancelAtPeriodEnd: boolean;
  cancelRequestedAt?: string | null;
  cancelledAt?: string | null;
  notes?: string | null;
  effectiveStatus: EffectiveSubscriptionStatus;
  isLicensed: boolean;
  isReadOnlyByLicense: boolean;
  daysRemaining: number;
  licenseMessage: string;
}

export interface SubscriptionPlanDto {
  id: string;
  name: string;
  tier?: string | null;
  monthlyPrice: number;
  currency: string;
  billingPeriod: BillingPeriod;
  introMonthlyPrice?: number | null;
  introPeriodCount?: number | null;
  isCustomQuote: boolean;
  isDefault: boolean;
  isActive: boolean;
  displayOrder: number;
  description?: string | null;
  featuresJson?: string | null;
  maxInstructors?: number | null;
  maxStudents?: number | null;
}

export type SubscriptionPlanInput = Omit<SubscriptionPlanDto, "id">;

export interface SubscriptionPaymentDto {
  id: string;
  amount: number;
  currency: string;
  billingPeriod: BillingPeriod;
  periodStart: string;
  periodEnd: string;
  paidAt: string;
  source: "Manual" | "Gateway" | "Adjustment";
  notes?: string | null;
}

export interface StudioSubscriptionViewDto {
  hasSubscription: boolean;
  planName?: string | null;
  tier?: string | null;
  status?: SubscriptionStatus | null;
  monthlyPrice?: number | null;
  currency?: string | null;
  billingPeriod?: BillingPeriod | null;
  currentPeriodStart?: string | null;
  currentPeriodEnd?: string | null;
  trialEndsAt?: string | null;
  cancelAtPeriodEnd: boolean;
  cancelRequestedAt?: string | null;
  effectiveStatus: EffectiveSubscriptionStatus;
  isLicensed: boolean;
  isReadOnlyByLicense: boolean;
  daysRemaining: number;
  licenseMessage: string;
  payments: SubscriptionPaymentDto[];
}

export interface PlatformStudioCountsDto {
  userCount: number;
  studentCount: number;
  activeStudentCount: number;
  guestStudentCount: number;
  instructorCount: number;
  sessionCountThisMonth: number;
  bookingCountThisMonth: number;
  paymentAmountThisMonth: number;
  overduePaymentAmount: number;
}

export interface PlatformActivityItemDto {
  kind: string;
  description: string;
  atUtc: string;
}

export interface PlatformStudioDetailDto {
  id: string;
  name: string;
  slug: string;
  status: StudioStatus;
  timeZoneId: string;
  currency: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  city?: string | null;
  createdAt: string;
  suspendedAt?: string | null;
  suspendedReason?: string | null;
  reactivatedAt?: string | null;
  owner: PlatformOwnerDto;
  subscription?: PlatformSubscriptionDto | null;
  counts: PlatformStudioCountsDto;
  recentActivity: PlatformActivityItemDto[];
}

export interface PlatformStudioStatusDto {
  id: string;
  status: StudioStatus;
  suspendedAt?: string | null;
  suspendedReason?: string | null;
  reactivatedAt?: string | null;
}

export interface PlatformHealthSummaryDto {
  databaseHealthy: boolean;
  totalStudios: number;
  activeStudios: number;
  suspendedStudios: number;
  notificationsPending: number;
  notificationsFailed: number;
  notificationsSentLast24h: number;
  lastNotificationActivityUtc?: string | null;
  reminderWorkerEnabled: boolean;
  reminderIntervalSeconds: number;
  missingSubscriptionCount: number;
  serverTimeUtc: string;
}

export interface SubscriptionInput {
  planName: string;
  tier?: string | null;
  status: SubscriptionStatus;
  monthlyPrice: number;
  currency: string;
  billingPeriod: BillingPeriod;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEndsAt?: string | null;
  notes?: string | null;
}
