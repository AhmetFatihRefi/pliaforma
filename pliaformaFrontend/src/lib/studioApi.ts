import { api } from "./api";
import type {
  AttendanceMarkInput,
  AttendanceStatus,
  AttendanceSummaryDto,
  BulkGenerateResult,
  JoinRequestDto,
  JoinCodeDto,
  MyJoinRequestDto,
  PackagePlanDto,
  PackagePlanInput,
  PackagePlanPriceVersionDto,
  PaymentDto,
  PaymentMethod,
  PaymentStatus,
  StudentPackageDetailDto,
  StudentPackageDto,
  ClassBookingDto,
  ClassBookingStatus,
  ClassSessionDto,
  ClassSessionKind,
  ClassSessionStatus,
  ClassTypeDto,
  ClassTypeInput,
  CreateClosureResult,
  DayOfWeek,
  GuestInput,
  InstructorAssignmentDto,
  InstructorAvailabilityDto,
  InstructorDashboardDto,
  InstructorSelfDto,
  InstructorsReportDto,
  MakeupPolicyDto,
  NotificationDto,
  PaymentsReportDto,
  ReminderRunResultDto,
  StudentDashboardDto,
  StudioDashboardDto,
  StudioSubscriptionViewDto,
  MemberDto,
  MyAttendanceDto,
  MyBookingDto,
  MyMakeupCreditsDto,
  PendingInvitationDto,
  StudentContractDto,
  ContractScheduleSlotInput,
  ContractSchedulePreviewDto,
  GenerateContractScheduleResultDto,
  SessionAttendanceDto,
  ConfirmTodayResultDto,
  RateType,
  StudentDto,
  StudentMakeupCreditsDto,
  MakeupEligibleStudentDto,
  StudentSessionDto,
  StudioClosureActionType,
  StudioClosureDto,
  StudioDto,
} from "./domain-types";
import type { StudioRole } from "./types";

export const studioService = {
  getMine: () => api<StudioDto>("/api/studio"),
  update: (input: Omit<StudioDto, "id" | "slug" | "isActive">) =>
    api<StudioDto>("/api/studio", { method: "PUT", body: input }),
};

export const classTypeService = {
  list: (onlyActive?: boolean) =>
    api<ClassTypeDto[]>(`/api/class-types${onlyActive ? "?onlyActive=true" : ""}`),
  create: (input: ClassTypeInput) => api<ClassTypeDto>("/api/class-types", { method: "POST", body: input }),
  update: (id: string, input: ClassTypeInput) =>
    api<ClassTypeDto>(`/api/class-types/${id}`, { method: "PUT", body: input }),
  remove: (id: string) => api<null>(`/api/class-types/${id}`, { method: "DELETE" }),
};

// Stüdyo yöneticisi tarafı üyelik yönetimi (/api/studio/memberships)
export const memberService = {
  list: (includeInactive?: boolean) =>
    api<MemberDto[]>(`/api/studio/memberships${includeInactive ? "?includeInactive=true" : ""}`),
  invite: (input: { email: string; role: StudioRole; displayName?: string; rate?: number; rateType?: RateType }) =>
    api<MemberDto>("/api/studio/memberships/invite", { method: "POST", body: input }),
  changeRole: (membershipId: string, role: StudioRole) =>
    api<null>(`/api/studio/memberships/${membershipId}/role`, { method: "PATCH", body: { role } }),
  remove: (membershipId: string) =>
    api<null>(`/api/studio/memberships/${membershipId}/remove`, { method: "POST", body: {} }),
  // Sprint 15: bekleyen katılım istekleri + katılım kodu (Owner/StudioAdmin)
  joinRequests: () => api<JoinRequestDto[]>("/api/studio/join-requests"),
  joinCode: () => api<JoinCodeDto>("/api/studio/join-code"),
  regenerateJoinCode: () => api<JoinCodeDto>("/api/studio/join-code/regenerate", { method: "POST", body: {} }),
};

// Sprint 18a: öğrenci sözleşmeleri + sözleşmeden program (/api/studio) — Owner/StudioAdmin
export const contractService = {
  forStudent: (studentId: string) => api<StudentContractDto[]>(`/api/studio/students/${studentId}/contracts`),
  create: (studentId: string, input: {
    classTypeId: string; instructorProfileId: string; startDate: string; endDate: string;
    totalLessonCount: number; makeupAllowance: number; price: number; currency?: string;
    isIndividualDefault: boolean; notes?: string | null;
  }) => api<StudentContractDto>(`/api/studio/students/${studentId}/contracts`, { method: "POST", body: input }),
  get: (id: string) => api<StudentContractDto>(`/api/studio/contracts/${id}`),
  cancel: (id: string) => api<null>(`/api/studio/contracts/${id}/cancel`, { method: "POST", body: {} }),
  previewSchedule: (id: string, slots: ContractScheduleSlotInput[]) =>
    api<ContractSchedulePreviewDto>(`/api/studio/contracts/${id}/schedule/preview`, { method: "POST", body: { slots } }),
  generateSchedule: (id: string, input: { slots: ContractScheduleSlotInput[]; reuseExistingGroup: boolean; individual: boolean | null }) =>
    api<GenerateContractScheduleResultDto>(`/api/studio/contracts/${id}/schedule/generate`, { method: "POST", body: input }),
};

// Kullanıcı tarafı üyelik aksiyonları (/api/members)
export const membershipService = {
  // Sprint 15: JoinCode + rol ile katılım başvurusu
  apply: (joinCode: string, role: StudioRole) =>
    api<PendingInvitationDto>("/api/members/apply", { method: "POST", body: { joinCode, role } }),
  myInvitations: () => api<PendingInvitationDto[]>("/api/members/my-invitations"),
  myJoinRequests: () => api<MyJoinRequestDto[]>("/api/members/my-join-requests"),
  accept: (membershipId: string) =>
    api<null>(`/api/members/${membershipId}/accept`, { method: "POST", body: {} }),
  reject: (membershipId: string) =>
    api<null>(`/api/members/${membershipId}/reject`, { method: "POST", body: {} }),
};

export const instructorService = {
  updateRate: (profileId: string, input: { rate: number; rateType: RateType }) =>
    api<null>(`/api/instructors/${profileId}/rate`, { method: "PUT", body: input }),
  classTypes: (profileId: string) =>
    api<InstructorAssignmentDto[]>(`/api/instructors/${profileId}/class-types`),
  assign: (profileId: string, input: { classTypeId: string; rateOverride?: number | null }) =>
    api<InstructorAssignmentDto>(`/api/instructors/${profileId}/class-types`, { method: "POST", body: input }),
  updateAssignment: (assignmentId: string, rateOverride: number | null) =>
    api<InstructorAssignmentDto>(`/api/instructors/class-types/${assignmentId}`, { method: "PUT", body: { rateOverride } }),
  unassign: (assignmentId: string) =>
    api<null>(`/api/instructors/class-types/${assignmentId}`, { method: "DELETE" }),
  me: () => api<InstructorSelfDto>("/api/instructors/me"),
};

// Hoca müsaitliği (/api/studio/...)
export const availabilityService = {
  list: (instructorProfileId?: string) =>
    api<InstructorAvailabilityDto[]>(`/api/studio/instructor-availabilities${instructorProfileId ? `?instructorProfileId=${instructorProfileId}` : ""}`),
  create: (instructorId: string, input: { dayOfWeek: DayOfWeek; startTime: string; endTime: string }) =>
    api<InstructorAvailabilityDto>(`/api/studio/instructors/${instructorId}/availabilities`, { method: "POST", body: input }),
  update: (id: string, input: { dayOfWeek: DayOfWeek; startTime: string; endTime: string; isActive: boolean }) =>
    api<InstructorAvailabilityDto>(`/api/studio/instructor-availabilities/${id}`, { method: "PUT", body: input }),
  remove: (id: string) => api<null>(`/api/studio/instructor-availabilities/${id}`, { method: "DELETE" }),
};

// Ders seansları (/api/studio/class-sessions)
export const classSessionService = {
  list: (params: { from?: string; to?: string; instructorProfileId?: string; classTypeId?: string; status?: ClassSessionStatus }) => {
    const q = new URLSearchParams();
    if (params.from) q.set("from", params.from);
    if (params.to) q.set("to", params.to);
    if (params.instructorProfileId) q.set("instructorProfileId", params.instructorProfileId);
    if (params.classTypeId) q.set("classTypeId", params.classTypeId);
    if (params.status) q.set("status", params.status);
    const qs = q.toString();
    return api<ClassSessionDto[]>(`/api/studio/class-sessions${qs ? `?${qs}` : ""}`);
  },
  create: (input: { instructorProfileId: string; classTypeId: string; startAt: string; capacity?: number; notes?: string; sessionKind?: ClassSessionKind }) =>
    api<ClassSessionDto>("/api/studio/class-sessions", { method: "POST", body: input }),
  bulkGenerate: (input: { instructorProfileId: string; classTypeId: string; date: string; rangeStart: string; rangeEnd: string; capacity?: number; breakMinutes?: number }) =>
    api<BulkGenerateResult>("/api/studio/class-sessions/bulk-generate", { method: "POST", body: input }),
  cancel: (id: string, input: { reason?: string; grantMakeupCredit?: boolean } = {}) =>
    api<null>(`/api/studio/class-sessions/${id}/cancel`, { method: "POST", body: input }),
  reschedule: (id: string, input: { newStartAt: string; reason?: string; keepBookings: boolean; grantMakeupCredit?: boolean }) =>
    api<ClassSessionDto>(`/api/studio/class-sessions/${id}/reschedule`, { method: "POST", body: input }),
  remove: (id: string) => api<null>(`/api/studio/class-sessions/${id}`, { method: "DELETE" }),
  // Sprint 19: ders onayı + fiilî hoca.
  confirm: (id: string) =>
    api<SessionAttendanceDto>(`/api/studio/class-sessions/${id}/confirm`, { method: "POST", body: {} }),
  confirmToday: () =>
    api<ConfirmTodayResultDto>("/api/studio/class-sessions/confirm-today", { method: "POST", body: {} }),
  setActualInstructor: (id: string, actualInstructorProfileId: string | null) =>
    api<null>(`/api/studio/class-sessions/${id}/actual-instructor`, { method: "POST", body: { actualInstructorProfileId } }),
  // Sprint 20: ders hoca nedeniyle işlenemedi → Cancelled + öğrencilere telafi.
  notDelivered: (id: string, reason?: string) =>
    api<null>(`/api/studio/class-sessions/${id}/not-delivered`, { method: "POST", body: { reason } }),
};

// Kapalı günler / toplu işlemler (/api/studio/closures) — Owner/StudioAdmin
export const closureService = {
  list: () => api<StudioClosureDto[]>("/api/studio/closures"),
  get: (id: string) => api<StudioClosureDto>(`/api/studio/closures/${id}`),
  create: (input: {
    title: string;
    reason?: string;
    startAt: string;
    endAt: string;
    actionType: StudioClosureActionType;
    offsetDays?: number | null;
    affectedInstructorProfileId?: string | null;
    affectedClassTypeId?: string | null;
  }) => api<CreateClosureResult>("/api/studio/closures", { method: "POST", body: input }),
};

// Telafi kredileri — personel tarafı (/api/studio)
export const makeupCreditService = {
  forStudent: (studentId: string) => api<StudentMakeupCreditsDto>(`/api/studio/students/${studentId}/makeup-credits`),
  // Sprint 20: telafi dersine eklenebilir (kullanılabilir hak > 0) öğrenciler.
  eligibleStudents: () => api<MakeupEligibleStudentDto[]>("/api/studio/makeup-eligible-students"),
  grant: (studentId: string, input: { amount: number; notes?: string }) =>
    api<StudentMakeupCreditsDto>(`/api/studio/students/${studentId}/makeup-credits/grant`, { method: "POST", body: input }),
  revoke: (studentId: string, input: { amount: number; notes?: string }) =>
    api<StudentMakeupCreditsDto>(`/api/studio/students/${studentId}/makeup-credits/revoke`, { method: "POST", body: input }),
  getPolicy: () => api<MakeupPolicyDto>("/api/studio/makeup-policy"),
  updatePolicy: (input: MakeupPolicyDto) =>
    api<MakeupPolicyDto>("/api/studio/makeup-policy", { method: "PUT", body: input }),
};

// Telafi kredileri — öğrenci self (/api/student)
export const studentMakeupCreditService = {
  mine: () => api<MyMakeupCreditsDto>("/api/student/makeup-credits"),
};

// Yoklama — personel tarafı (/api/studio)
export const attendanceService = {
  forSession: (sessionId: string) => api<SessionAttendanceDto>(`/api/studio/class-sessions/${sessionId}/attendance`),
  markBatch: (sessionId: string, items: AttendanceMarkInput[]) =>
    api<SessionAttendanceDto>(`/api/studio/class-sessions/${sessionId}/attendance`, { method: "PUT", body: { items } }),
  markSingle: (bookingId: string, input: { status: AttendanceStatus; notes?: string; grantMakeupCredit?: boolean }) =>
    api<null>(`/api/studio/bookings/${bookingId}/attendance`, { method: "PUT", body: input }),
  studentSummary: (studentId: string, month?: string) =>
    api<AttendanceSummaryDto>(`/api/studio/students/${studentId}/attendance/summary${month ? `?month=${month}` : ""}`),
};

// Yoklama — öğrenci self (/api/student)
export const studentAttendanceService = {
  mine: () => api<MyAttendanceDto[]>("/api/student/attendance"),
  summary: (month?: string) =>
    api<AttendanceSummaryDto>(`/api/student/attendance/summary${month ? `?month=${month}` : ""}`),
};

// Paket planları (/api/studio/package-plans) — Owner/StudioAdmin
export const packagePlanService = {
  list: (onlyActive?: boolean) => api<PackagePlanDto[]>(`/api/studio/package-plans${onlyActive ? "?onlyActive=true" : ""}`),
  get: (id: string) => api<PackagePlanDto>(`/api/studio/package-plans/${id}`),
  create: (input: PackagePlanInput) =>
    api<PackagePlanDto>("/api/studio/package-plans", { method: "POST", body: input }),
  update: (id: string, input: PackagePlanInput) =>
    api<PackagePlanDto>(`/api/studio/package-plans/${id}`, { method: "PUT", body: input }),
  remove: (id: string) => api<null>(`/api/studio/package-plans/${id}`, { method: "DELETE" }),
  // Sprint 14: fiyat versiyonları
  priceVersions: (id: string) => api<PackagePlanPriceVersionDto[]>(`/api/studio/package-plans/${id}/price-versions`),
  addPriceVersion: (id: string, input: { price: number; effectiveFrom: string }) =>
    api<PackagePlanPriceVersionDto>(`/api/studio/package-plans/${id}/price-versions`, { method: "POST", body: input }),
};

// Öğrenci paketleri (/api/studio) — Owner/StudioAdmin
export const studentPackageService = {
  forStudent: (studentId: string) => api<StudentPackageDto[]>(`/api/studio/students/${studentId}/packages`),
  sell: (studentId: string, input: { packagePlanId?: string | null; name?: string; creditCount?: number; price?: number; validityDays?: number | null }) =>
    api<StudentPackageDto>(`/api/studio/students/${studentId}/packages`, { method: "POST", body: input }),
  get: (id: string) => api<StudentPackageDetailDto>(`/api/studio/student-packages/${id}`),
  cancel: (id: string) => api<null>(`/api/studio/student-packages/${id}/cancel`, { method: "POST", body: {} }),
  adjust: (id: string, input: { amount: number; notes?: string }) =>
    api<StudentPackageDto>(`/api/studio/student-packages/${id}/adjust`, { method: "POST", body: input }),
};

// Ödemeler (/api/studio) — Owner/StudioAdmin
export const paymentService = {
  list: (params: { studentProfileId?: string; status?: PaymentStatus } = {}) => {
    const q = new URLSearchParams();
    if (params.studentProfileId) q.set("studentProfileId", params.studentProfileId);
    if (params.status) q.set("status", params.status);
    const qs = q.toString();
    return api<PaymentDto[]>(`/api/studio/payments${qs ? `?${qs}` : ""}`);
  },
  forStudent: (studentId: string) => api<PaymentDto[]>(`/api/studio/students/${studentId}/payments`),
  create: (studentId: string, input: { studentPackageId?: string | null; amount: number; currency?: string; dueDate: string; method?: PaymentMethod | null; notes?: string }) =>
    api<PaymentDto>(`/api/studio/students/${studentId}/payments`, { method: "POST", body: input }),
  update: (id: string, input: { amount: number; dueDate: string; method?: PaymentMethod | null; notes?: string }) =>
    api<PaymentDto>(`/api/studio/payments/${id}`, { method: "PUT", body: input }),
  markPaid: (id: string, method?: PaymentMethod | null) =>
    api<null>(`/api/studio/payments/${id}/mark-paid`, { method: "POST", body: { method } }),
  cancel: (id: string) => api<null>(`/api/studio/payments/${id}/cancel`, { method: "POST", body: {} }),
};

// Paket/ödeme — öğrenci self (/api/student)
export const studentFinanceService = {
  myPackages: () => api<StudentPackageDto[]>("/api/student/packages"),
  myPayments: () => api<PaymentDto[]>("/api/student/payments"),
};

// Dashboard & raporlar (/api/studio, /api/instructor, /api/student)
const monthQ = (m?: string) => (m ? `?month=${m}` : "");
export const dashboardService = {
  studio: (month?: string) => api<StudioDashboardDto>(`/api/studio/dashboard${monthQ(month)}`),
  instructorById: (instructorId: string, month?: string) =>
    api<InstructorDashboardDto>(`/api/studio/instructors/${instructorId}/dashboard${monthQ(month)}`),
  studentById: (studentId: string, month?: string) =>
    api<StudentDashboardDto>(`/api/studio/students/${studentId}/dashboard${monthQ(month)}`),
  myInstructor: (month?: string) => api<InstructorDashboardDto>(`/api/instructor/dashboard${monthQ(month)}`),
  myStudent: (month?: string) => api<StudentDashboardDto>(`/api/student/dashboard${monthQ(month)}`),
};
export const reportService = {
  payments: (month?: string, status?: PaymentStatus) => {
    const q = new URLSearchParams();
    if (month) q.set("month", month);
    if (status) q.set("status", status);
    const qs = q.toString();
    return api<PaymentsReportDto>(`/api/studio/reports/payments${qs ? `?${qs}` : ""}`);
  },
  // Sprint 19: ay VEYA tarih aralığı (from/to).
  instructors: (params?: { month?: string; from?: string; to?: string }) => {
    const q = new URLSearchParams();
    if (params?.month) q.set("month", params.month);
    if (params?.from) q.set("from", params.from);
    if (params?.to) q.set("to", params.to);
    const qs = q.toString();
    return api<InstructorsReportDto>(`/api/studio/reports/instructors${qs ? `?${qs}` : ""}`);
  },
};

// Bildirimler (/api/notifications kullanıcı self + /api/studio/notifications manager trigger)
export const notificationService = {
  mine: () => api<NotificationDto[]>("/api/notifications"),
  unreadCount: () => api<number>("/api/notifications/unread-count"),
  markRead: (id: string) => api<null>(`/api/notifications/${id}/read`, { method: "POST", body: {} }),
  markAllRead: () => api<null>("/api/notifications/read-all", { method: "POST", body: {} }),
  runReminders: () => api<ReminderRunResultDto>("/api/studio/notifications/run-reminders", { method: "POST", body: {} }),
};

// Stüdyo aboneliği — Owner self (/api/studio/subscription)
export const studioSubscriptionService = {
  mine: () => api<StudioSubscriptionViewDto>("/api/studio/subscription"),
  cancel: () => api<StudioSubscriptionViewDto>("/api/studio/subscription/cancel", { method: "POST", body: {} }),
};

// Rezervasyon — personel tarafı (/api/studio)
export const bookingService = {
  list: (params: { classSessionId?: string; status?: ClassBookingStatus } = {}) => {
    const q = new URLSearchParams();
    if (params.classSessionId) q.set("classSessionId", params.classSessionId);
    if (params.status) q.set("status", params.status);
    const qs = q.toString();
    return api<ClassBookingDto[]>(`/api/studio/bookings${qs ? `?${qs}` : ""}`);
  },
  forSession: (sessionId: string) => api<ClassBookingDto[]>(`/api/studio/class-sessions/${sessionId}/bookings`),
  create: (sessionId: string, studentProfileId: string) =>
    api<ClassBookingDto>(`/api/studio/class-sessions/${sessionId}/bookings`, { method: "POST", body: { studentProfileId } }),
  cancel: (id: string) => api<null>(`/api/studio/bookings/${id}/cancel`, { method: "POST", body: {} }),
};

// Rezervasyon — öğrenci self (/api/student)
export const studentBookingService = {
  myBookings: () => api<MyBookingDto[]>("/api/student/bookings"),
  bookableSessions: (params: { from?: string; to?: string; classTypeId?: string } = {}) => {
    const q = new URLSearchParams();
    if (params.from) q.set("from", params.from);
    if (params.to) q.set("to", params.to);
    if (params.classTypeId) q.set("classTypeId", params.classTypeId);
    const qs = q.toString();
    return api<StudentSessionDto[]>(`/api/student/class-sessions${qs ? `?${qs}` : ""}`);
  },
  book: (sessionId: string) => api<ClassBookingDto>(`/api/student/class-sessions/${sessionId}/book`, { method: "POST", body: {} }),
  cancel: (bookingId: string) => api<null>(`/api/student/bookings/${bookingId}/cancel`, { method: "POST", body: {} }),
};

// Öğrenci yönetimi (/api/studio/students) + öğrenci self (/api/student/me)
export const studentService = {
  list: () => api<StudentDto[]>("/api/studio/students"),
  search: (query: string) => api<StudentDto[]>(`/api/studio/students/search?query=${encodeURIComponent(query)}`),
  get: (id: string) => api<StudentDto>(`/api/studio/students/${id}`),
  createGuest: (input: GuestInput) => api<StudentDto>("/api/studio/students/guest", { method: "POST", body: input }),
  update: (id: string, input: GuestInput) => api<StudentDto>(`/api/studio/students/${id}`, { method: "PUT", body: input }),
  convertToUser: (id: string, input: { email: string; initialPassword?: string }) =>
    api<StudentDto>(`/api/studio/students/${id}/convert-to-user`, { method: "POST", body: input }),
  deactivate: (id: string) => api<null>(`/api/studio/students/${id}/deactivate`, { method: "POST", body: {} }),
  reactivate: (id: string) => api<null>(`/api/studio/students/${id}/reactivate`, { method: "POST", body: {} }),
  me: () => api<StudentDto>("/api/student/me"),
};
