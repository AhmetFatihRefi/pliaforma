using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Platform;

internal static class SubscriptionMapping
{
    /// <summary>Aboneliği DTO'ya çevirir + dinamik lisansı hesaplar (today = UTC gün).</summary>
    public static PlatformSubscriptionDto ToDto(PlatformSubscription x, DateOnly today)
    {
        var lic = SubscriptionRules.Evaluate(x, today);
        return new PlatformSubscriptionDto(
            x.Id, x.StudioId, x.SubscriptionPlanId, x.PlanName, x.Tier, x.Status, x.MonthlyPrice,
            x.IntroMonthlyPrice, x.IntroPeriodsRemaining, x.Currency, x.BillingPeriod,
            x.CurrentPeriodStart, x.CurrentPeriodEnd, x.TrialEndsAt, x.CancelAtPeriodEnd, x.CancelRequestedAt,
            x.CancelledAt, x.Notes,
            lic.EffectiveStatus, lic.IsLicensed, lic.IsReadOnlyByLicense, lic.DaysRemaining, lic.LicenseMessage);
    }
}

// ---- Aboneliği getir (PlatformAdmin) ----
public sealed record GetStudioSubscriptionQuery(Guid StudioId) : IQuery<PlatformSubscriptionDto?>;

public sealed class GetStudioSubscriptionHandler : IQueryHandler<GetStudioSubscriptionQuery, PlatformSubscriptionDto?>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformSubscriptionDto?> Handle(GetStudioSubscriptionQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var sub = await _db.PlatformSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudioId == request.StudioId, ct);
        return sub is null ? null : SubscriptionMapping.ToDto(sub, DateOnly.FromDateTime(_clock.UtcNow));
    }
}

// ---- Aboneliği güncelle (upsert; geriye dönük uyumluluk — Sprint 10) ----
/// <summary>Serbest abonelik upsert (PlatformAdmin). Plan ata / ödeme kaydet akışları tercih edilir.</summary>
public sealed record UpdateStudioSubscriptionCommand(
    Guid StudioId, string PlanName, string? Tier, SubscriptionStatus Status, decimal MonthlyPrice,
    string Currency, BillingPeriod BillingPeriod, DateOnly CurrentPeriodStart, DateOnly CurrentPeriodEnd,
    DateOnly? TrialEndsAt, string? Notes) : ICommand<PlatformSubscriptionDto>;

public sealed class UpdateStudioSubscriptionValidator : AbstractValidator<UpdateStudioSubscriptionCommand>
{
    public UpdateStudioSubscriptionValidator()
    {
        RuleFor(x => x.StudioId).NotEmpty();
        RuleFor(x => x.PlanName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).MaximumLength(50);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.CurrentPeriodEnd).GreaterThanOrEqualTo(x => x.CurrentPeriodStart)
            .WithMessage("Dönem bitişi başlangıçtan önce olamaz.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class UpdateStudioSubscriptionHandler : ICommandHandler<UpdateStudioSubscriptionCommand, PlatformSubscriptionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public UpdateStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformSubscriptionDto> Handle(UpdateStudioSubscriptionCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var studioExists = await _db.Studios.AnyAsync(s => s.Id == request.StudioId, ct);
        if (!studioExists)
            throw new NotFoundException("Stüdyo", request.StudioId);

        var sub = await _db.PlatformSubscriptions.FirstOrDefaultAsync(x => x.StudioId == request.StudioId, ct);
        if (sub is null)
        {
            sub = new PlatformSubscription { StudioId = request.StudioId };
            _db.PlatformSubscriptions.Add(sub);
        }

        sub.PlanName = request.PlanName.Trim();
        sub.Tier = request.Tier?.Trim();
        sub.Status = request.Status;
        sub.MonthlyPrice = request.MonthlyPrice;
        sub.Currency = request.Currency.Trim().ToUpperInvariant();
        sub.BillingPeriod = request.BillingPeriod;
        sub.CurrentPeriodStart = request.CurrentPeriodStart;
        sub.CurrentPeriodEnd = request.CurrentPeriodEnd;
        sub.TrialEndsAt = request.TrialEndsAt;
        sub.Notes = request.Notes?.Trim();

        await _db.SaveChangesAsync(ct);
        return SubscriptionMapping.ToDto(sub, DateOnly.FromDateTime(_clock.UtcNow));
    }
}

// ---- Aboneliği iptal et (cancel-at-period-end; PlatformAdmin) ----
/// <summary>
/// Aboneliği DÖNEM SONUNDA iptal eder: Status DEĞİŞMEZ, CancelAtPeriodEnd=true, CurrentPeriodEnd korunur →
/// dönem sonuna kadar lisanslı (effectiveStatus=CancelPending), sonra Expired. (Owner self-cancel ile aynı.)
/// </summary>
public sealed record CancelStudioSubscriptionCommand(Guid StudioId) : ICommand<PlatformSubscriptionDto>;

public sealed class CancelStudioSubscriptionHandler : ICommandHandler<CancelStudioSubscriptionCommand, PlatformSubscriptionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public CancelStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PlatformSubscriptionDto> Handle(CancelStudioSubscriptionCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var sub = await _db.PlatformSubscriptions.FirstOrDefaultAsync(x => x.StudioId == request.StudioId, ct)
            ?? throw new NotFoundException("Abonelik", request.StudioId);

        sub.CancelAtPeriodEnd = true;
        sub.CancelRequestedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        return SubscriptionMapping.ToDto(sub, DateOnly.FromDateTime(_clock.UtcNow));
    }
}
