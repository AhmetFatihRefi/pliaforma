using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Platform;

/// <summary>
/// Tüm stüdyoların listesi (PlatformAdmin). Opsiyonel arama (ad/slug) + durum filtresi.
/// Sayımlar GroupBy ile toplu çekilir (N+1 yok).
/// </summary>
public sealed record GetPlatformStudiosQuery(string? Search, StudioStatus? Status) : IQuery<IReadOnlyList<PlatformStudioListItemDto>>;

public sealed class GetPlatformStudiosHandler : IQueryHandler<GetPlatformStudiosQuery, IReadOnlyList<PlatformStudioListItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetPlatformStudiosHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PlatformStudioListItemDto>> Handle(GetPlatformStudiosQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var nowUtc = _clock.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var monthStartDate = DateOnly.FromDateTime(monthStart);
        var monthEndDate = DateOnly.FromDateTime(monthEnd);

        var studioQuery = _db.Studios.AsNoTracking().AsQueryable();
        if (request.Status is { } st)
            studioQuery = studioQuery.Where(s => s.Status == st);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            studioQuery = studioQuery.Where(s => s.Name.ToLower().Contains(term) || s.Slug.ToLower().Contains(term));
        }

        var studios = await (
            from s in studioQuery
            join o in _db.Users.AsNoTracking() on s.OwnerUserId equals o.Id into owners
            from o in owners.DefaultIfEmpty()
            orderby s.CreatedAt descending
            select new
            {
                s.Id, s.Name, s.Slug, s.Status, s.CreatedAt,
                OwnerName = o != null ? o.FullName : "—",
                OwnerEmail = o != null ? o.Email : "—",
            }).ToListAsync(ct);

        var ids = studios.Select(s => s.Id).ToList();
        if (ids.Count == 0)
            return [];

        var userCounts = await _db.StudioMemberships.AsNoTracking()
            .Where(m => ids.Contains(m.StudioId) && m.Status == MembershipStatus.Active)
            .GroupBy(m => m.StudioId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var studentCounts = await _db.StudentProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.StudioId))
            .GroupBy(p => p.StudioId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var instructorCounts = await _db.InstructorProfiles.AsNoTracking()
            .Where(i => ids.Contains(i.StudioId))
            .GroupBy(i => i.StudioId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var bookingCounts = await _db.ClassBookings.AsNoTracking()
            .Where(bk => ids.Contains(bk.StudioId) && bk.Status == ClassBookingStatus.Booked
                         && bk.CreatedAt >= monthStart && bk.CreatedAt < monthEnd)
            .GroupBy(bk => bk.StudioId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var paymentSums = await _db.Payments.AsNoTracking()
            .Where(p => ids.Contains(p.StudioId) && p.DueDate >= monthStartDate && p.DueDate < monthEndDate)
            .GroupBy(p => p.StudioId).Select(g => new { g.Key, S = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var subs = await _db.PlatformSubscriptions.AsNoTracking()
            .Where(x => ids.Contains(x.StudioId))
            .Select(x => new { x.StudioId, x.Status, x.PlanName }).ToListAsync(ct);

        var userMap = userCounts.ToDictionary(x => x.Key, x => x.C);
        var studentMap = studentCounts.ToDictionary(x => x.Key, x => x.C);
        var instructorMap = instructorCounts.ToDictionary(x => x.Key, x => x.C);
        var bookingMap = bookingCounts.ToDictionary(x => x.Key, x => x.C);
        var paymentMap = paymentSums.ToDictionary(x => x.Key, x => x.S);
        var subMap = subs.ToDictionary(x => x.StudioId, x => x);

        return studios.Select(s =>
        {
            subMap.TryGetValue(s.Id, out var sub);
            return new PlatformStudioListItemDto(
                s.Id, s.Name, s.Slug, s.Status, s.OwnerName, s.OwnerEmail,
                userMap.GetValueOrDefault(s.Id), studentMap.GetValueOrDefault(s.Id),
                instructorMap.GetValueOrDefault(s.Id), bookingMap.GetValueOrDefault(s.Id),
                paymentMap.GetValueOrDefault(s.Id),
                sub?.Status, sub?.PlanName, s.CreatedAt);
        }).ToList();
    }
}
