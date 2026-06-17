using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Platform;

/// <summary>Tek stüdyo detayı (PlatformAdmin): temel bilgi + sahip + abonelik + sayımlar + kısa son aktivite.</summary>
public sealed record GetPlatformStudioDetailQuery(Guid StudioId) : IQuery<PlatformStudioDetailDto>;

public sealed class GetPlatformStudioDetailHandler : IQueryHandler<GetPlatformStudioDetailQuery, PlatformStudioDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetPlatformStudioDetailHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformStudioDetailDto> Handle(GetPlatformStudioDetailQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var sid = request.StudioId;
        var studio = await _db.Studios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct)
            ?? throw new NotFoundException("Stüdyo", sid);

        var owner = await _db.Users.AsNoTracking()
            .Where(u => u.Id == studio.OwnerUserId)
            .Select(u => new PlatformOwnerDto(u.Id, u.FullName, u.Email, u.Phone))
            .FirstOrDefaultAsync(ct)
            ?? new PlatformOwnerDto(studio.OwnerUserId, "—", "—", null);

        var sub = await _db.PlatformSubscriptions.AsNoTracking()
            .Where(x => x.StudioId == sid)
            .Select(x => new PlatformSubscriptionDto(
                x.Id, x.StudioId, x.PlanName, x.Tier, x.Status, x.MonthlyPrice, x.Currency,
                x.BillingPeriod, x.CurrentPeriodStart, x.CurrentPeriodEnd, x.TrialEndsAt, x.CancelledAt, x.Notes))
            .FirstOrDefaultAsync(ct);

        var nowUtc = _clock.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var monthStartDate = DateOnly.FromDateTime(monthStart);
        var monthEndDate = DateOnly.FromDateTime(monthEnd);
        var todayUtc = DateOnly.FromDateTime(nowUtc);

        var userCount = await _db.StudioMemberships.AsNoTracking().CountAsync(m => m.StudioId == sid && m.Status == MembershipStatus.Active, ct);
        var studentRows = await _db.StudentProfiles.AsNoTracking().Where(p => p.StudioId == sid).Select(p => p.Status).ToListAsync(ct);
        var instructorCount = await _db.InstructorProfiles.AsNoTracking().CountAsync(i => i.StudioId == sid, ct);
        var sessionsThisMonth = await _db.ClassSessions.AsNoTracking()
            .CountAsync(s => s.StudioId == sid && s.CreatedAt >= monthStart && s.CreatedAt < monthEnd, ct);
        var bookingsThisMonth = await _db.ClassBookings.AsNoTracking()
            .CountAsync(b => b.StudioId == sid && b.Status == ClassBookingStatus.Booked && b.CreatedAt >= monthStart && b.CreatedAt < monthEnd, ct);
        var paymentAmountThisMonth = await _db.Payments.AsNoTracking()
            .Where(p => p.StudioId == sid && p.DueDate >= monthStartDate && p.DueDate < monthEndDate)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var overdueAmount = await _db.Payments.AsNoTracking()
            .Where(p => p.StudioId == sid && p.Status == PaymentStatus.Pending && p.PaidAt == null && p.DueDate < todayUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var counts = new PlatformStudioCountsDto(
            userCount,
            studentRows.Count,
            studentRows.Count(s => s == StudentStatus.Active),
            studentRows.Count(s => s == StudentStatus.Guest),
            instructorCount,
            sessionsThisMonth,
            bookingsThisMonth,
            paymentAmountThisMonth,
            overdueAmount);

        // Kısa son aktivite — birkaç kaynaktan az kayıt, in-memory birleştirilip zamana göre sıralanır.
        var activity = new List<PlatformActivityItemDto>();

        var recentPayments = await _db.Payments.AsNoTracking().Where(p => p.StudioId == sid)
            .OrderByDescending(p => p.CreatedAt).Take(5)
            .Select(p => new { p.Amount, p.Currency, p.Status, p.CreatedAt }).ToListAsync(ct);
        activity.AddRange(recentPayments.Select(p =>
            new PlatformActivityItemDto("Payment", $"Ödeme {p.Amount:0.##} {p.Currency} ({p.Status})", p.CreatedAt)));

        var recentNotifications = await _db.Notifications.AsNoTracking().Where(n => n.StudioId == sid)
            .OrderByDescending(n => n.CreatedAt).Take(5)
            .Select(n => new { n.Title, n.CreatedAt }).ToListAsync(ct);
        activity.AddRange(recentNotifications.Select(n =>
            new PlatformActivityItemDto("Notification", n.Title, n.CreatedAt)));

        var recentSessions = await _db.ClassSessions.AsNoTracking().Where(s => s.StudioId == sid)
            .OrderByDescending(s => s.CreatedAt).Take(5)
            .Select(s => new { Name = s.ClassType!.Name, s.StartAt, s.CreatedAt }).ToListAsync(ct);
        activity.AddRange(recentSessions.Select(s =>
            new PlatformActivityItemDto("Session", $"Seans {s.Name} ({s.StartAt:yyyy-MM-dd HH:mm})", s.CreatedAt)));

        var recentActivity = activity.OrderByDescending(a => a.AtUtc).Take(10).ToList();

        return new PlatformStudioDetailDto(
            studio.Id, studio.Name, studio.Slug, studio.Status, studio.TimeZoneId, studio.Currency,
            studio.ContactEmail, studio.ContactPhone, studio.City, studio.CreatedAt,
            studio.SuspendedAt, studio.SuspendedReason, studio.ReactivatedAt,
            owner, sub, counts, recentActivity);
    }
}
