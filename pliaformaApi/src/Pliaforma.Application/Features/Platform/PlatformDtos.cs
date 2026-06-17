using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Platform;

/// <summary>Platform geneli özet (tüm stüdyolar). Basit count/summary; ağır sorgu yok.</summary>
public sealed record PlatformOverviewDto(
    int TotalStudios,
    int ActiveStudios,
    int SuspendedStudios,
    int CancelledStudios,
    int TotalUsers,
    int TotalStudents,
    int TotalInstructors,
    int TotalBookingsThisMonth,
    int TotalPaymentsThisMonth,
    decimal OverduePaymentAmount,
    int NotificationsPending,
    int NotificationsFailed,
    DateTime? LastNotificationActivityUtc,
    bool DatabaseHealthy,
    string PeriodLabel);

/// <summary>Stüdyo liste satırı (platform admin). Finansal/sayısal özetler dahil.</summary>
public sealed record PlatformStudioListItemDto(
    Guid Id,
    string Name,
    string Slug,
    StudioStatus Status,
    string OwnerName,
    string OwnerEmail,
    int UserCount,
    int StudentCount,
    int InstructorCount,
    int BookingCountThisMonth,
    decimal PaymentAmountThisMonth,
    SubscriptionStatus? SubscriptionStatus,
    string? PlanName,
    DateTime CreatedAt);

public sealed record PlatformOwnerDto(Guid UserId, string FullName, string Email, string? Phone);

public sealed record PlatformSubscriptionDto(
    Guid Id,
    Guid StudioId,
    string PlanName,
    string? Tier,
    SubscriptionStatus Status,
    decimal MonthlyPrice,
    string Currency,
    BillingPeriod BillingPeriod,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd,
    DateOnly? TrialEndsAt,
    DateTime? CancelledAt,
    string? Notes);

public sealed record PlatformStudioCountsDto(
    int UserCount,
    int StudentCount,
    int ActiveStudentCount,
    int GuestStudentCount,
    int InstructorCount,
    int SessionCountThisMonth,
    int BookingCountThisMonth,
    decimal PaymentAmountThisMonth,
    decimal OverduePaymentAmount);

public sealed record PlatformActivityItemDto(string Kind, string Description, DateTime AtUtc);

/// <summary>Stüdyo detayı (platform admin): temel bilgi + sahip + abonelik + sayımlar + kısa son aktivite.</summary>
public sealed record PlatformStudioDetailDto(
    Guid Id,
    string Name,
    string Slug,
    StudioStatus Status,
    string TimeZoneId,
    string Currency,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    DateTime CreatedAt,
    DateTime? SuspendedAt,
    string? SuspendedReason,
    DateTime? ReactivatedAt,
    PlatformOwnerDto Owner,
    PlatformSubscriptionDto? Subscription,
    PlatformStudioCountsDto Counts,
    IReadOnlyList<PlatformActivityItemDto> RecentActivity);

/// <summary>Sistem/gözlemlenebilirlik özeti — READ-ONLY. Sistemi değiştirecek işlem yapmaz.</summary>
public sealed record PlatformHealthSummaryDto(
    bool DatabaseHealthy,
    int TotalStudios,
    int ActiveStudios,
    int SuspendedStudios,
    int NotificationsPending,
    int NotificationsFailed,
    int NotificationsSentLast24h,
    DateTime? LastNotificationActivityUtc,
    bool ReminderWorkerEnabled,
    int ReminderIntervalSeconds,
    DateTime ServerTimeUtc);
