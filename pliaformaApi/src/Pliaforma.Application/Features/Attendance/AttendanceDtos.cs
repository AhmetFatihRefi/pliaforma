using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>Öğrencinin aktif paketi özeti (yalnızca Owner/StudioAdmin görür — finans gizliliği).</summary>
public sealed record StudentPackageBriefDto(Guid Id, string Name, int CurrentCreditBalance, DateOnly? ValidUntil);

/// <summary>Bir seansın yoklama listesinde tek satır (booking + öğrenci + yoklama durumu + paket bilgisi).</summary>
public sealed record AttendanceItemDto(
    Guid BookingId,
    Guid StudentProfileId,
    string StudentName,
    AttendanceStatus? Status,
    string? Notes,
    bool CreditGranted,
    bool PackageConsumed,
    DateTime? MarkedAt,
    IReadOnlyList<StudentPackageBriefDto>? Packages);

/// <summary>Seans bazlı yoklama görünümü (seans bilgisi + aktif rezervasyonlar).</summary>
public sealed record SessionAttendanceDto(
    Guid SessionId,
    string ClassTypeName,
    string InstructorName,
    DateTime StartAt,
    DateTime EndAt,
    ClassSessionStatus Status,
    bool CanMark,
    IReadOnlyList<AttendanceItemDto> Items);

/// <summary>Batch/single yoklama işaretleme girişi.</summary>
public sealed record AttendanceMarkInput(
    Guid BookingId,
    AttendanceStatus Status,
    string? Notes = null,
    bool GrantMakeupCredit = false,
    bool ConsumePackageCredit = false,
    Guid? StudentPackageId = null);

/// <summary>Öğrencinin kendi katılım geçmişinde tek kayıt.</summary>
public sealed record MyAttendanceDto(
    Guid Id,
    Guid ClassSessionId,
    string ClassTypeName,
    string InstructorName,
    DateTime StartAt,
    DateTime EndAt,
    AttendanceStatus Status,
    bool CreditGranted,
    string? Notes);

/// <summary>Aylık katılım özeti (basit sayımlar).</summary>
public sealed record AttendanceSummaryDto(
    string Month,
    int PresentCount,
    int AbsentCount,
    int ExcusedCount,
    int LateCancelCount,
    int TotalMarked)
{
    /// <summary>Katılım oranı = Present / TotalMarked. Hiç işaretleme yoksa null.</summary>
    public double? AttendanceRate => TotalMarked == 0 ? null : Math.Round((double)PresentCount / TotalMarked, 4);
}
