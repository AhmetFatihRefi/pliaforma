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
    public static PlatformSubscriptionDto ToDto(PlatformSubscription x) => new(
        x.Id, x.StudioId, x.PlanName, x.Tier, x.Status, x.MonthlyPrice, x.Currency,
        x.BillingPeriod, x.CurrentPeriodStart, x.CurrentPeriodEnd, x.TrialEndsAt, x.CancelledAt, x.Notes);
}

// ---- Aboneliği getir ----
/// <summary>Stüdyonun aboneliğini getirir (PlatformAdmin). Abonelik yoksa null döner.</summary>
public sealed record GetStudioSubscriptionQuery(Guid StudioId) : IQuery<PlatformSubscriptionDto?>;

public sealed class GetStudioSubscriptionHandler : IQueryHandler<GetStudioSubscriptionQuery, PlatformSubscriptionDto?>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PlatformSubscriptionDto?> Handle(GetStudioSubscriptionQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var sub = await _db.PlatformSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudioId == request.StudioId, ct);
        return sub is null ? null : SubscriptionMapping.ToDto(sub);
    }
}

// ---- Aboneliği güncelle (upsert) ----
/// <summary>
/// Stüdyonun aboneliğini günceller; yoksa oluşturur (upsert). PlatformAdmin. Gerçek tahsilat YOK;
/// yalnızca takip modeli. Bir stüdyonun tek aboneliği olur (filtered unique index).
/// </summary>
public sealed record UpdateStudioSubscriptionCommand(
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
    string? Notes) : ICommand<PlatformSubscriptionDto>;

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

    public UpdateStudioSubscriptionHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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
        // Cancelled dışı bir duruma getirilirse iptal zamanını temizle.
        sub.CancelledAt = request.Status == SubscriptionStatus.Cancelled ? (sub.CancelledAt ?? null) : null;

        await _db.SaveChangesAsync(ct);
        return SubscriptionMapping.ToDto(sub);
    }
}

// ---- Aboneliği iptal et ----
/// <summary>Aboneliği iptal eder (Status=Cancelled + CancelledAt). PlatformAdmin. Kayıt silinmez.</summary>
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

        sub.Status = SubscriptionStatus.Cancelled;
        sub.CancelledAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        return SubscriptionMapping.ToDto(sub);
    }
}
