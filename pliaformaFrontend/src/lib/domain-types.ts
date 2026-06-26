// Sprint 1 backend DTO'larının birebir karşılığı (senkron sözleşme).
import type { StudioRole, MembershipStatus, GlobalRole } from "./types";

export type EquipmentType = "Mat" | "Reformer" | "Cadillac" | "Chair" | "Barrel";
export type ClassCapacityType = "Private" | "Duo" | "Group";
export type ClassLevel = "Beginner" | "Intermediate" | "Advanced" | "AllLevels";
export type ClassPurpose = "General" | "ClinicalRehab" | "Prenatal";
export type RateType = "PerSession" | "PerHour" | "PerStudentPerSession";
export type MembershipDirection = "InvitedByStudio" | "RequestedByUser";

// Sprint 28A: ders tipi sadeleştirildi → equipment/capacityType/maxCapacity/level/purpose KALDIRILDI.
// Sprint 32A: opsiyonel ekipman havuzu bağı (equipmentId) + UI rozeti için equipmentName eklendi.
export interface ClassTypeDto {
  id: string;
  name: string;
  defaultDurationMinutes: number;
  description?: string | null;
  isActive: boolean;
  equipmentId?: string | null;
  equipmentName?: string | null;
}

// equipmentName salt-okunur (sunucudan gelir); gönderilmez. equipmentId opsiyonel gönderilir.
export type ClassTypeInput = Omit<ClassTypeDto, "id" | "equipmentName"> & { isActive?: boolean };

// Sprint 32A: Ekipman/envanter havuzu. TotalCapacity = quantity * perUnitConcurrentUsage (kişi).
export interface EquipmentDto {
  id: string;
  name: string;
  description?: string | null;
  quantity: number;
  perUnitConcurrentUsage: number;
  totalCapacity: number;
  isActive: boolean;
  boundClassTypeCount: number;
}

export interface BoundClassTypeDto {
  id: string;
  name: string;
}

export interface EquipmentDetailDto {
  id: string;
  name: string;
  description?: string | null;
  quantity: number;
  perUnitConcurrentUsage: number;
  totalCapacity: number;
  isActive: boolean;
  boundClassTypes: BoundClassTypeDto[];
}

export interface EquipmentInput {
  name: string;
  description?: string | null;
  quantity: number;
  perUnitConcurrentUsage: number;
  isActive?: boolean;
}

// Sprint 32C: Boşluk bulma motoru (window-based). availableEquipmentCapacity null → ekipman kısıtı yok.
export interface AvailabilityInstructorDto {
  id: string;
  name: string;
}

export interface AvailabilityWindowDto {
  date: string; // YYYY-MM-DD
  startWindowFrom: string; // ISO (en erken başlangıç)
  startWindowTo: string; // ISO (en geç başlangıç)
  durationMinutes: number;
  requiredCapacity: number;
  availableEquipmentCapacity?: number | null;
  availableInstructors: AvailabilityInstructorDto[];
}

export interface AvailabilityQuery {
  classTypeId: string;
  date?: string;
  from?: string;
  to?: string;
  durationMinutes: number;
  requiredCapacity: number;
  instructorId?: string;
  incrementMinutes?: number;
}

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
// Sprint 28A: seviye artık öğrenci özelliği (opsiyonel).
export type StudentLevel = "Beginner" | "Intermediate" | "Advanced";

export interface StudentDto {
  id: string;
  userId?: string | null;
  studioMembershipId?: string | null;
  fullName: string;
  phone?: string | null;
  email?: string | null;
  status: StudentStatus;
  source: StudentSource;
  level?: StudentLevel | null;
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
  level?: StudentLevel | null;
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
export const studentLevelLabels: Record<StudentLevel, string> = {
  Beginner: "Başlangıç", Intermediate: "Orta", Advanced: "İleri",
};

// Sprint 29A: line tercih gün/saati (gün zorunlu, saat opsiyonel). preferredStartTime "HH:mm:ss" | null.
export interface PreferredSlotDto {
  id: string;
  dayOfWeek: DayOfWeek;
  preferredStartTime?: string | null;
  isActive: boolean;
  sequenceNo: number;
  notes?: string | null;
}
export interface PreferredSlotInput {
  dayOfWeek: DayOfWeek;
  preferredStartTime?: string | null;
  notes?: string | null;
}

// Sprint 28A: sözleşme satırı (read-only). Line editör/ekleme 28B.
export type StudentContractLineStatus = "Active" | "Completed" | "Cancelled" | "Expired";
export interface StudentContractLineDto {
  id: string;
  studentContractId: string;
  studentProfileId: string;
  studentName: string;
  classTypeId: string;
  classTypeName: string;
  // Sprint 33: hoca opsiyonel (null = herhangi bir hoca).
  instructorProfileId?: string | null;
  instructorName?: string | null;
  startDate: string;
  endDate: string;
  lessonCount: number;
  scheduledLessonCount: number;
  availableLessonCount: number;
  pricePerLesson: number;
  totalPrice: number;
  paid: number;
  remaining: number;
  makeupAllowance: number;
  dueDate?: string | null;
  isIndividual: boolean;
  // Sprint 33: kapasite tercihi (null = farketmez; 1 = bireysel; N = en fazla N).
  preferredMaxCapacity?: number | null;
  status: StudentContractLineStatus;
  currency: string;
  sequenceNo: number;
  // Sprint 29C: ders hakkı tüketimi. scheduledLessonCount = planned + delivered (reserved);
  // availableLessonCount = AvailableForPlanning ("Planlanabilir"); remainingLessonRight = max(0, lessonCount − delivered).
  plannedLessonCount: number;
  deliveredLessonCount: number;
  remainingLessonRight: number;
  // Sprint 29A: öğrencinin tercih ettiği gün/saatler (gün zorunlu, saat opsiyonel).
  preferredSlots: PreferredSlotDto[];
}

// ===== Sprint 3: Ders Seansları (Sprint 26: hoca müsaitlik DTO'su kaldırıldı — modül ana üründen çıktı) =====
export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";
export type ClassSessionStatus = "Scheduled" | "Cancelled" | "Completed";

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
export interface GenerateContractScheduleResultDto { createdCount: number; newSessionCount: number; reusedSessionCount: number; skippedCount: number; createdStartAts: string[]; skippedOutsideWorkingHoursCount: number }

// ===== Sprint 34: tercih güncelle + kalan dersleri yeniden planla =====
// Tercih güncelle (full-replace slot listesi). Mevcut programa dokunmaz.
export interface ContractLinePreferencesInput {
  instructorProfileId?: string | null;
  preferredMaxCapacity?: number | null;
  preferredSlots: PreferredSlotInput[];
}
export interface ReplanLinePreviewDto {
  deliveredCount: number;
  remainingLessonRight: number;
  futureBookingsToCancel: number;
  plannableCount: number;
  notPlannableCount: number;
  effectiveInstructorProfileId?: string | null;
  effectiveInstructorName?: string | null;
  effectiveCapacity?: number | null;
  slots: ContractScheduleSlotPreviewDto[];
  warnings: string[];
}
export interface ReplanLineResultDto {
  cancelledFutureBookings: number;
  emptiedSessionsCancelled: number;
  createdCount: number;
  newSessionCount: number;
  reusedSessionCount: number;
  skippedCount: number;
  notPlannedCount: number;
  createdStartAts: string[];
  skippedOutsideWorkingHoursCount: number;
}

// ===== Sprint 37: Stüdyo çalışma saatleri (haftalık) =====
export interface WorkingHourDayDto {
  dayOfWeek: DayOfWeek;
  isClosed: boolean;
  opensAt?: string | null;   // "HH:mm:ss" | null
  closesAt?: string | null;
}
// isConfigured=false → stüdyo henüz kaydetmedi; days fallback 07:00–23:00 (uygulanan + öneri).
export interface StudioWorkingHoursDto { isConfigured: boolean; days: WorkingHourDayDto[] }
export interface WorkingHourDayInput {
  dayOfWeek: DayOfWeek;
  isClosed: boolean;
  opensAt?: string | null;
  closesAt?: string | null;
}

// ===== Sprint 23: Sözleşme ek işlemi (amendment) + borç =====
export type ContractAmendmentKind = "ExtraLessons" | "DateExtension" | "PriceAdjustment" | "Mixed" | "Correction" | "Other";
export const contractAmendmentKindLabels: Record<ContractAmendmentKind, string> = {
  ExtraLessons: "Ek ders", DateExtension: "Tarih uzatma", PriceAdjustment: "Ücret düzeltme",
  Mixed: "Karma", Correction: "Düzeltme", Other: "Diğer",
};
export const contractAmendmentKinds: ContractAmendmentKind[] = ["ExtraLessons", "DateExtension", "PriceAdjustment", "Mixed", "Correction", "Other"];
export interface ContractAmendmentDto {
  id: string;
  studentContractId: string;
  kind: ContractAmendmentKind;
  additionalLessonCount: number;
  previousTotalLessonCount: number;
  newTotalLessonCount: number;
  previousEndDate: string;
  newEndDate: string;
  additionalPrice: number;
  currency: string;
  description?: string | null;
  createdByName: string;
  createdAt: string;
}
// Sprint 28B: borç LINE-bazlı. Kalemler StudentContractLineDto; özet currency bazında; amendment KATILMAZ.
export interface StudentDebtSummaryDto {
  byCurrency: CurrencyDebtDto[];
  lines: StudentContractLineDto[];
  unassignedByCurrency: CurrencyPaidDto[];
}

// ===== Sprint 24: Öğrenci self-servis (salt-okunur) trimmed görünümler =====
export interface StudentAmendmentDto {
  kind: ContractAmendmentKind;
  additionalLessonCount: number;
  datesExtended: boolean;
  newEndDate: string;
  additionalPrice: number;
  currency: string;
  description?: string | null;
  createdAt: string;
}
export interface StudentContractViewDto {
  id: string;
  classTypeName: string;
  instructorName: string;
  startDate: string;
  endDate: string;
  totalLessonCount: number;
  scheduledLessonCount: number;
  availableLessonCount: number;
  status: StudentContractStatus;
  amendments: StudentAmendmentDto[];
}
export interface CurrencyDebtDto {
  currency: string;
  total: number;
  paid: number;
  remaining: number;
}
export interface CurrencyPaidDto { currency: string; paid: number }
export interface MyDebtSummaryDto {
  byCurrency: CurrencyDebtDto[];
  lines: StudentContractLineDto[];
  unassignedByCurrency: CurrencyPaidDto[];
}

// Sprint 28B: yeni line (ek alım) girdisi.
export interface ContractLineInput {
  headerId?: string | null;
  classTypeId: string;
  // Sprint 33: hoca opsiyonel (null = herhangi bir hoca).
  instructorProfileId?: string | null;
  startDate: string;
  endDate: string;
  lessonCount: number;
  pricePerLesson: number;
  makeupAllowance: number;
  dueDate?: string | null;
  // Sprint 33: "Bireysel mi?" yerine kapasite tercihi (null = farketmez; 1 = bireysel; N = en fazla N).
  preferredMaxCapacity?: number | null;
  currency?: string | null;
  notes?: string | null;
  // Sprint 29A: yeni line'da EN AZ 1 tercih gün/saat zorunlu (aynı gün+saat tekrar edemez).
  preferredSlots: PreferredSlotInput[];
  // Sprint 38: opsiyonel ödeme planı (peşin = 1 taksit; taksitli = 2+). Σ amount == toplam ücret.
  paymentPlan?: InstallmentInput[] | null;
}

// ===== Sprint 38: Ödeme planı / taksit takibi =====
export interface InstallmentInput { dueDate: string; amount: number; notes?: string | null }
export type InstallmentStatus = "Planned" | "PartiallyPaid" | "Paid" | "Overdue";
export const installmentStatusLabels: Record<InstallmentStatus, string> = {
  Planned: "Bekliyor", PartiallyPaid: "Kısmi", Paid: "Ödendi", Overdue: "Gecikti",
};
export type PaymentPlanKind = "None" | "Cash" | "Installment";
export interface PaymentInstallmentDto {
  id: string;
  sequenceNo: number;
  dueDate: string;
  amount: number;
  paidAmount: number;
  status: InstallmentStatus;
  notes?: string | null;
}
export interface ContractLinePaymentPlanDto {
  lineId: string;
  currency: string;
  isConfigured: boolean;       // false → plan yok (eski davranış)
  planType: PaymentPlanKind;   // None / Cash / Installment
  totalPlannedAmount: number;
  totalPaidAmount: number;
  remainingAmount: number;
  overdueAmount: number;
  overpaidAmount: number;      // yalnız bilgi (iade/credit yok)
  nextDueDate?: string | null;
  nextDueAmount?: number | null;
  paidCount: number;
  partialCount: number;
  overdueCount: number;
  upcomingCount: number;
  installments: PaymentInstallmentDto[];
}

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
  // Sprint 28C-2: hareketin bağlı olduğu line.
  studentContractLineId?: string | null;
  lineLabel?: string | null;
}

// Sprint 20: kazanılan / kullanılan / kullanılabilir özeti.
export interface MakeupCreditSummaryDto {
  earned: number;
  used: number;
  available: number;
}

// Sprint 28C-2: line bazlı telafi kırılımı.
export interface MakeupCreditByLineDto {
  lineId: string;
  classTypeId: string;
  classTypeName: string;
  lineLabel: string;
  earned: number;
  used: number;
  available: number;
  isIndividual: boolean;
  startDate: string;
  endDate: string;
}

export interface StudentMakeupCreditsDto {
  studentProfileId: string;
  studentName: string;
  balance: number;
  summary: MakeupCreditSummaryDto;
  byLine: MakeupCreditByLineDto[];
  transactions: MakeupCreditTransactionDto[];
}

export interface MyMakeupCreditsDto {
  balance: number;
  summary: MakeupCreditSummaryDto;
  byLine: MakeupCreditByLineDto[];
  transactions: MakeupCreditTransactionDto[];
}

// Sprint 28C-2: telafi dersine/talebine uygun LINE (staff booking + request eligible ortak).
export interface MakeupEligibleLineDto {
  lineId: string;
  studentProfileId: string;
  studentName: string;
  classTypeId: string;
  classTypeName: string;
  instructorProfileId: string;
  instructorName: string;
  lineLabel: string;
  availableCredits: number;
  level?: StudentLevel | null;
  isIndividual: boolean;
  startDate: string;
  endDate: string;
}

// Sprint 29B: Regular seansa aday öğrenci (öğrenci + uygun line). Aynı öğrencinin çok uygun line'ı → çok satır.
export interface RegularCandidateDto {
  studentProfileId: string;
  studentName: string;
  studentLevel?: StudentLevel | null;
  lineId: string;
  lineLabel: string;
  classTypeName: string;
  availableForPlanning: number;
  isIndividual: boolean;
  matchedDay: boolean;
  hasPreferredSlots: boolean;
  nearestPreferredTime?: string | null; // "HH:mm:ss"
  minutesFromSession?: number | null;
}
// Sprint 29B: makeup aday = mevcut MakeupEligibleLineDto (session-scoped: classType + çakışma server-side).
export type MakeupCandidateDto = MakeupEligibleLineDto;

// Sprint 27: telafi dersi talebi (Instructor Makeup Request & Pending Schedule Blocks).
export type MakeupRequestStatus = "Pending" | "Approved" | "Rejected" | "Withdrawn";

export const makeupRequestStatusLabels: Record<MakeupRequestStatus, string> = {
  Pending: "Onay bekliyor", Approved: "Onaylandı", Rejected: "Reddedildi", Withdrawn: "Geri çekildi",
};

export interface MakeupRequestDto {
  id: string;
  studentProfileId: string;
  studentName: string;
  requestedInstructorProfileId: string;
  instructorName: string;
  classTypeId: string;
  classTypeName: string;
  startAt: string; // ISO (studio-yerel duvar saati)
  endAt: string;
  status: MakeupRequestStatus;
  description?: string | null;
  requestedByUserId: string;
  decisionByUserId?: string | null;
  decidedAt?: string | null;
  decisionNote?: string | null;
  resultingClassSessionId?: string | null;
  resultingBookingId?: string | null;
  createdAt: string;
  /** Yalnız Pending kayıtlarda hesaplanır (owner ekranı çakışma durumu). */
  hasConflict: boolean;
  conflictReason?: string | null;
}

// Sprint 21: zincirleme kaydırma önizleme/sonuç.
export interface ChainShiftItemDto {
  sessionId: string;
  classTypeName: string;
  oldStartAt: string;
  oldEndAt: string;
  newStartAt: string;
  newEndAt: string;
  conflictReason?: string | null; // "past" | "instructor" | "student" (Sprint 26: availability yok)
}
export interface ChainShiftPreviewDto {
  affected: ChainShiftItemDto[];
  canApply: boolean;
}

// Sprint 22: köklü grup düzeni değişikliği.
export interface GroupChangeCandidateDto {
  lineId: string; // Sprint 28C-1: grup düzeni değişikliği line-bazlı
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
  lineId: string; // Sprint 28C-1
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
  studentContractId?: string | null;
  // Sprint 28B: line bağı + etiket ("ClassTypeName · start–end"); null → "Diğer ödeme".
  studentContractLineId?: string | null;
  lineLabel?: string | null;
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
  // Sprint 30: maaş/ödeme kırılımı
  planned: number;
  delivered: number;
  upcoming: number;
  notDelivered: number;
  gaveSubstitution: number;
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
  | "MembershipInvite" | "LinePaymentDue";
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
  LinePaymentDue: "Ödeme hatırlatması",
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
  // Sprint 28D: line ödeme hatırlatması + throttle/skip sayaçları.
  linePaymentDue: number;
  deferred: number;
  skippedGuest: number;
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

// Sprint 40: kullanıcıya görünen rol/üyelik/audit metinleri (yalnız UI; backend enum value'ları sabit).
export const studioRoleLabels: Record<StudioRole, string> = {
  Owner: "Sahip",
  StudioAdmin: "Stüdyo Yöneticisi",
  Instructor: "Eğitmen",
  Student: "Öğrenci",
};

export const globalRoleLabels: Record<GlobalRole, string> = {
  Standard: "Standart",
  PlatformAdmin: "Platform Yöneticisi",
};

export const membershipStatusLabels: Record<MembershipStatus, string> = {
  Pending: "Bekliyor",
  Active: "Aktif",
  Rejected: "Reddedildi",
  Removed: "Çıkarıldı",
};

export const securityAuditOutcomeLabels: Record<string, string> = {
  Success: "Başarılı",
  Failure: "Başarısız",
};

// Güvenlik denetimi olay tipleri. Backend yeni tip eklerse → ham değere fallback (aşağıdaki helper).
export const securityAuditEventTypeLabels: Record<string, string> = {
  LoginSucceeded: "Giriş başarılı",
  LoginFailed: "Giriş başarısız",
  AccountLockedOut: "Hesap kilitlendi",
  Logout: "Çıkış",
  PasswordResetRequested: "Şifre sıfırlama istendi",
  PasswordResetCompleted: "Şifre sıfırlandı",
  PasswordChanged: "Şifre değiştirildi",
  MFASetupStarted: "MFA kurulumu başladı",
  MFASetupCompleted: "MFA kurulumu tamamlandı",
  MFAChallengeCreated: "MFA doğrulaması oluşturuldu",
  MFASucceeded: "MFA başarılı",
  MFAFailed: "MFA başarısız",
  MFARecoveryCodeUsed: "MFA kurtarma kodu kullanıldı",
  MFAResetCompleted: "MFA sıfırlandı",
  RoleChanged: "Rol değiştirildi",
  MemberInvited: "Üye davet edildi",
  MembershipAccepted: "Üyelik kabul edildi",
  MembershipRejected: "Üyelik reddedildi",
  MemberRemoved: "Üye çıkarıldı",
  PaymentCreated: "Ödeme oluşturuldu",
  PaymentUpdated: "Ödeme güncellendi",
  PaymentMarkedPaid: "Ödeme 'ödendi' işaretlendi",
  PaymentCancelled: "Ödeme iptal edildi",
  ContractCreated: "Sözleşme oluşturuldu",
  ContractAmended: "Sözleşme değiştirildi",
  ContractCancelled: "Sözleşme iptal edildi",
  StudentDataListed: "Öğrenci verisi listelendi",
  HealthNotesAccessed: "Sağlık notlarına erişildi",
};

/** Bilinen anahtar için Türkçe; bilinmeyende ham değeri döndürür (güvenli fallback). */
export function securityAuditEventLabel(eventType: string): string {
  return securityAuditEventTypeLabels[eventType] ?? eventType;
}
export function securityAuditOutcomeLabel(outcome: string): string {
  return securityAuditOutcomeLabels[outcome] ?? outcome;
}

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

// Sprint 31C-1 — Güvenlik denetim görüntüleyici (PlatformAdmin, salt-okunur, cross-tenant).
// EventType/Outcome backend'de string olarak döner (HasConversion<string>).
export interface SecurityAuditLogRowDto {
  id: string;
  occurredAt: string;
  eventType: string;
  outcome: string;
  actorUserId?: string | null;
  actorEmailMasked?: string | null;
  studioId?: string | null;
  studioName?: string | null;
  targetType?: string | null;
  targetId?: string | null;
  ip?: string | null;
  userAgent?: string | null;
  detail?: string | null;
}

export interface SecurityAuditLogPageDto {
  items: SecurityAuditLogRowDto[];
  page: number;
  pageSize: number;
  total: number;
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
