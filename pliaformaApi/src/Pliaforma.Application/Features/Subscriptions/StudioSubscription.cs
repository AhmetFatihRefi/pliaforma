using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Platform;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Subscriptions;

/// <summary>Owner'ın kendi stüdyosunun abonelik/lisans görünümü + tahsilat geçmişi (yalnız Owner).</summary>
internal static class StudioSubscriptionGuard
{
    public static void EnsureOwner(ICurrentUserService u)
    {
        if (u.StudioRole != StudioRole.Owner)
            throw new ForbiddenException("Bu işlem yalnızca stüdyo sahibi (Owner) tarafından yapılabilir.");
    }

    /// <summary>Stüdyonun abonelik görünümünü kurar (sub + ödeme geçmişi + dinamik lisans).</summary>
    public static async Task<StudioSubscriptionViewDto> BuildViewAsync(
        IAppDbContext db, Guid sid, DateOnly today, CancellationToken ct)
    {
        var sub = await db.PlatformSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.StudioId == sid, ct);
        var lic = SubscriptionRules.Evaluate(sub, today);
        var payments = sub is null
            ? new List<SubscriptionPaymentDto>()
            : (await db.PlatformSubscriptionPayments.AsNoTracking()
                    .Where(p => p.StudioId == sid).OrderByDescending(p => p.PaidAt).Take(50).ToListAsync(ct))
                .Select(SubscriptionPaymentMapping.ToDto).ToList();

        return new StudioSubscriptionViewDto(
            HasSubscription: sub is not null,
            sub?.PlanName, sub?.Tier, sub?.Status, sub?.MonthlyPrice, sub?.Currency, sub?.BillingPeriod,
            sub?.CurrentPeriodStart, sub?.CurrentPeriodEnd, sub?.TrialEndsAt,
            sub?.CancelAtPeriodEnd ?? false, sub?.CancelRequestedAt,
            lic.EffectiveStatus, lic.IsLicensed, lic.IsReadOnlyByLicense, lic.DaysRemaining, lic.LicenseMessage,
            payments);
    }
}

// ---- Kendi aboneliğimi getir ----
public sealed record GetMyStudioSubscriptionQuery : IQuery<StudioSubscriptionViewDto>;

public sealed class GetMyStudioSubscriptionHandler : IQueryHandler<GetMyStudioSubscriptionQuery, StudioSubscriptionViewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetMyStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<StudioSubscriptionViewDto> Handle(GetMyStudioSubscriptionQuery request, CancellationToken ct)
    {
        StudioSubscriptionGuard.EnsureOwner(_currentUser);
        var sid = _currentUser.RequireStudioId();
        return await StudioSubscriptionGuard.BuildViewAsync(_db, sid, DateOnly.FromDateTime(_clock.UtcNow), ct);
    }
}

// ---- Kendi aboneliğimi iptal et (cancel-at-period-end) ----
/// <summary>Owner kendi aboneliğini dönem sonunda iptal eder. License-gated DEĞİL (her zaman iptal edilebilir).</summary>
public sealed record CancelMyStudioSubscriptionCommand : ICommand<StudioSubscriptionViewDto>;

public sealed class CancelMyStudioSubscriptionHandler : ICommandHandler<CancelMyStudioSubscriptionCommand, StudioSubscriptionViewDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public CancelMyStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<StudioSubscriptionViewDto> Handle(CancelMyStudioSubscriptionCommand request, CancellationToken ct)
    {
        StudioSubscriptionGuard.EnsureOwner(_currentUser);
        var sid = _currentUser.RequireStudioId();

        var sub = await _db.PlatformSubscriptions.FirstOrDefaultAsync(x => x.StudioId == sid, ct)
            ?? throw new NotFoundException("Abonelik", sid);

        sub.CancelAtPeriodEnd = true;
        sub.CancelRequestedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await StudioSubscriptionGuard.BuildViewAsync(_db, sid, DateOnly.FromDateTime(_clock.UtcNow), ct);
    }
}
