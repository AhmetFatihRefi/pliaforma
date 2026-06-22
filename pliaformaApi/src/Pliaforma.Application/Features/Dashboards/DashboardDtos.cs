namespace Pliaforma.Application.Features.Dashboards;

/// <summary>Katılım sayım dökümü (ay bazlı; dashboard'larda gömülü kullanılır).</summary>
public sealed record AttendanceBreakdownDto(
    int PresentCount, int AbsentCount, int ExcusedCount, int LateCancelCount, int TotalMarked)
{
    public double? AttendanceRate => TotalMarked == 0 ? null : Math.Round((double)PresentCount / TotalMarked, 4);
}

public sealed record PaymentTotalsDto(
    decimal PaidAmount, decimal PendingAmount, decimal OverdueAmount,
    int PaidCount, int PendingCount, int OverdueCount);

public sealed record PackageTotalsDto(int ActivePackageCount, int UsedCreditCount, int RemainingCreditTotal);

public sealed record MakeupTotalsDto(int TotalOutstandingBalance, int GrantedThisMonth, int UsedThisMonth);

public sealed record UpcomingDto(int TodaySessions, int UpcomingSessionsNext7Days);

// ---- Studio Dashboard ----
public sealed record StudioDashboardDto(
    string Month,
    int TotalStudents, int ActiveStudents, int GuestStudents, int NewStudentsThisMonth,
    int TotalInstructors,
    int SessionsThisMonth, int CompletedOrPastSessions, int CancelledSessions,
    int BookingsThisMonth,
    AttendanceBreakdownDto Attendance,
    PaymentTotalsDto Payments,
    PackageTotalsDto Packages,
    MakeupTotalsDto MakeupCredits,
    UpcomingDto Upcoming);

// ---- Instructor Dashboard ----
public sealed record ClassTypeCountDto(Guid ClassTypeId, string ClassTypeName, int SessionCount);

// Sprint 19: parasal alanlar (EstimatedEarning/EarningCalculationNote) kaldırıldı — hoca ücreti kapsam dışı.
public sealed record InstructorDashboardDto(
    string Month,
    Guid InstructorProfileId,
    string InstructorName,
    int SessionsThisMonth, int CancelledSessions, int BookingsCount,
    int PresentCount, int AbsentCount, int ExcusedCount,
    int TotalStudentsTaught,
    IReadOnlyList<ClassTypeCountDto> ClassTypeBreakdown);

// ---- Student / Staff-student Dashboard ----
public sealed record StudentMiniBookingDto(Guid BookingId, Guid ClassSessionId, string ClassTypeName, string InstructorName, DateTime StartAt, DateTime EndAt);
public sealed record StudentMiniAttendanceDto(Guid Id, string ClassTypeName, DateTime StartAt, string Status);
public sealed record StudentMiniMakeupDto(Guid Id, int Amount, string Type, string Reason, DateTime CreatedAt);

public sealed record StudentPackageSummaryDto(int ActivePackages, int TotalRemainingCredits, DateOnly? NearestPackageExpiry);
public sealed record StudentPaymentSummaryDto(decimal PendingAmount, decimal OverdueAmount, DateOnly? NextDueDate);

public sealed record StudentDashboardDto(
    string Month,
    Guid StudentProfileId,
    string StudentName,
    string Status,
    IReadOnlyList<StudentMiniBookingDto> UpcomingBookings,
    AttendanceBreakdownDto AttendanceSummary,
    StudentPackageSummaryDto PackageSummary,
    StudentPaymentSummaryDto? PaymentSummary,   // Instructor staff-view'da null (finans gizli)
    int MakeupCreditBalance,
    IReadOnlyList<StudentMiniMakeupDto> RecentMakeupTransactions,
    IReadOnlyList<StudentMiniAttendanceDto> RecentAttendance,
    IReadOnlyList<StudentMiniBookingDto> RecentBookings);

// ---- Reports ----
// Sprint 19: parasız & operasyonel. Ders atfı FİİLÎ hocaya (ActualInstructorProfileId ?? InstructorProfileId).
public sealed record InstructorReportRowDto(
    Guid InstructorProfileId, string InstructorName,
    int Sessions,             // verilen (held) ders sayısı
    int GroupSessions,        // grup dersleri
    int IndividualSessions,   // bireysel dersler
    int DistinctStudents,     // ders verilen farklı (Present) öğrenci sayısı
    int SubstitutionSessions, // bu hocanın fiilen verdiği ama planlananın farklı olduğu ders (devraldığı)
    // Sprint 30: stüdyo yetkilisinin maaş/ödeme hesabı için ek kırılım (biz para hesaplamayız; sayıları veririz).
    int Planned,              // ay içinde bu hocaya ATANAN tüm dersler (her durum)
    int Delivered,            // İŞLENEN (Completed, fiilî hocaya göre) — ödeme temeli
    int Upcoming,             // yaklaşan (Scheduled, gelecekte)
    int NotDelivered,         // hoca kaynaklı iptal (işlenmedi → telafi üretmiş)
    int GaveSubstitution,     // planlanan bu hoca ama dersi BAŞKA hoca verdi (devrettiği)
    IReadOnlyList<ClassTypeCountDto> ClassTypeBreakdown);
