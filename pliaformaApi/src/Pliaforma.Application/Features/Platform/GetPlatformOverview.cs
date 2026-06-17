using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Platform;

/// <summary>Platform geneli özet (PlatformAdmin). Cross-tenant; basit count/summary. Stüdyo bağlamı gerekmez.</summary>
public sealed record GetPlatformOverviewQuery : IQuery<PlatformOverviewDto>;

public sealed class GetPlatformOverviewHandler : IQueryHandler<GetPlatformOverviewQuery, PlatformOverviewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetPlatformOverviewHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformOverviewDto> Handle(GetPlatformOverviewQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var nowUtc = _clock.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var monthStartDate = DateOnly.FromDateTime(monthStart);
        var monthEndDate = DateOnly.FromDateTime(monthEnd);
        var todayUtc = DateOnly.FromDateTime(nowUtc);

        // Stüdyo durum dağılımı (tek tarama, in-memory grupla).
        var statuses = await _db.Studios.AsNoTracking().Select(s => s.Status).ToListAsync(ct);
        var totalStudios = statuses.Count;
        var activeStudios = statuses.Count(s => s == StudioStatus.Active);
        var suspendedStudios = statuses.Count(s => s == StudioStatus.Suspended);
        var cancelledStudios = statuses.Count(s => s == StudioStatus.Cancelled);

        var totalUsers = await _db.Users.AsNoTracking().CountAsync(ct);
        var totalStudents = await _db.StudentProfiles.AsNoTracking().CountAsync(ct);
        var totalInstructors = await _db.InstructorProfiles.AsNoTracking().CountAsync(ct);

        // Bu ay (UTC audit) oluşturulan aktif rezervasyonlar.
        var bookingsThisMonth = await _db.ClassBookings.AsNoTracking()
            .CountAsync(b => b.Status == ClassBookingStatus.Booked && b.CreatedAt >= monthStart && b.CreatedAt < monthEnd, ct);

        // Bu ay vadeli ödeme kaydı sayısı.
        var paymentsThisMonth = await _db.Payments.AsNoTracking()
            .CountAsync(p => p.DueDate >= monthStartDate && p.DueDate < monthEndDate, ct);

        // Tüm stüdyolarda effective-overdue toplam tutarı.
        var overdueAmount = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending && p.PaidAt == null && p.DueDate < todayUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var notificationsPending = await _db.Notifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Pending, ct);
        var notificationsFailed = await _db.Notifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Failed, ct);
        var lastNotificationActivity = await _db.Notifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt).Select(n => (DateTime?)n.CreatedAt).FirstOrDefaultAsync(ct);

        // Abonelik kaydı olmayan stüdyolar (fail-open lisanslı — görünür).
        var subStudioCount = await _db.PlatformSubscriptions.AsNoTracking().Select(s => s.StudioId).Distinct().CountAsync(ct);
        var missingSubscriptionCount = Math.Max(totalStudios - subStudioCount, 0);
        // Bu ay manuel abonelik tahsilatı (ledger'dan).
        var manualRevenueThisMonth = await _db.PlatformSubscriptionPayments.AsNoTracking()
            .Where(p => p.Source == PlatformSubscriptionPaymentSource.Manual && p.PaidAt >= monthStart && p.PaidAt < monthEnd)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new PlatformOverviewDto(
            totalStudios, activeStudios, suspendedStudios, cancelledStudios,
            totalUsers, totalStudents, totalInstructors,
            bookingsThisMonth, paymentsThisMonth, overdueAmount,
            notificationsPending, notificationsFailed, lastNotificationActivity,
            DatabaseHealthy: true,
            missingSubscriptionCount, manualRevenueThisMonth,
            PeriodLabel: $"{nowUtc:yyyy-MM} (UTC)");
    }
}
