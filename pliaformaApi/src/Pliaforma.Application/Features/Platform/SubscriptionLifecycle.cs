using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Platform;

internal static class SubscriptionPaymentMapping
{
    public static SubscriptionPaymentDto ToDto(PlatformSubscriptionPayment p) => new(
        p.Id, p.Amount, p.Currency, p.BillingPeriod, p.PeriodStart, p.PeriodEnd, p.PaidAt, p.Source, p.Notes);
}

// ---- Plan ata (snapshot) ----
/// <summary>Stüdyoya katalog planını atar; plan alanlarını aboneliğe SNAPSHOT'lar. Abonelik yoksa 14g trial ile oluşturur.</summary>
public sealed record AssignSubscriptionPlanCommand(Guid StudioId, Guid PlanId) : ICommand<PlatformSubscriptionDto>;

public sealed class AssignSubscriptionPlanHandler : ICommandHandler<AssignSubscriptionPlanCommand, PlatformSubscriptionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public AssignSubscriptionPlanHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PlatformSubscriptionDto> Handle(AssignSubscriptionPlanCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        if (!await _db.Studios.AnyAsync(s => s.Id == request.StudioId, ct))
            throw new NotFoundException("Stüdyo", request.StudioId);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct)
            ?? throw new NotFoundException("Plan", request.PlanId);

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var sub = await _db.PlatformSubscriptions.FirstOrDefaultAsync(x => x.StudioId == request.StudioId, ct);
        if (sub is null)
        {
            sub = new PlatformSubscription
            {
                StudioId = request.StudioId,
                Status = SubscriptionStatus.Trialing,
                CurrentPeriodStart = today,
                CurrentPeriodEnd = today.AddDays(SubscriptionRules.TrialDays),
                TrialEndsAt = today.AddDays(SubscriptionRules.TrialDays),
            };
            _db.PlatformSubscriptions.Add(sub);
        }

        // Plan snapshot (mevcut dönem/durum korunur).
        sub.SubscriptionPlanId = plan.Id;
        sub.PlanName = plan.Name;
        sub.Tier = plan.Tier;
        sub.MonthlyPrice = plan.MonthlyPrice;
        sub.Currency = plan.Currency;
        sub.BillingPeriod = plan.BillingPeriod;
        sub.IntroMonthlyPrice = plan.IntroMonthlyPrice;
        sub.IntroPeriodsRemaining = plan.IntroPeriodCount;

        await _db.SaveChangesAsync(ct);
        return SubscriptionMapping.ToDto(sub, today);
    }
}

// ---- Ödeme kaydet (ledger + dönem ilerlet, atomik) ----
/// <summary>
/// Abonelik tahsilatını kaydeder: PlatformSubscriptionPayment satırı + dönem ilerletme AYNI transaction'da.
/// Intro fiyatı (kalan varsa) otomatik uygulanır. Status=Active; bekleyen cancel temizlenir.
/// </summary>
public sealed record RecordSubscriptionPaymentCommand(Guid StudioId, decimal? Amount, string? Notes)
    : ICommand<PlatformSubscriptionDto>;

public sealed class RecordSubscriptionPaymentValidator : AbstractValidator<RecordSubscriptionPaymentCommand>
{
    public RecordSubscriptionPaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).When(x => x.Amount.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class RecordSubscriptionPaymentHandler : ICommandHandler<RecordSubscriptionPaymentCommand, PlatformSubscriptionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public RecordSubscriptionPaymentHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PlatformSubscriptionDto> Handle(RecordSubscriptionPaymentCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var sub = await _db.PlatformSubscriptions.FirstOrDefaultAsync(x => x.StudioId == request.StudioId, ct)
            ?? throw new ConflictException("Bu stüdyoya önce bir plan atayın (abonelik kaydı yok).");

        var nowUtc = _clock.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc);

        // Dönem: süresi dolmadıysa mevcut dönem sonundan, dolduysa bugünden devam.
        var baseStart = sub.CurrentPeriodEnd >= today ? sub.CurrentPeriodEnd : today;
        var newEnd = SubscriptionRules.AddPeriod(baseStart, sub.BillingPeriod);

        // Fiyat: explicit amount > intro (kalan varsa) > standart.
        decimal amount;
        var usedIntro = false;
        if (request.Amount.HasValue)
            amount = request.Amount.Value;
        else if (sub.IntroPeriodsRemaining is > 0 && sub.IntroMonthlyPrice.HasValue)
        {
            amount = sub.IntroMonthlyPrice.Value;
            usedIntro = true;
        }
        else
            amount = sub.MonthlyPrice;

        _db.PlatformSubscriptionPayments.Add(new PlatformSubscriptionPayment
        {
            StudioId = sub.StudioId,
            PlatformSubscriptionId = sub.Id,
            Amount = amount,
            Currency = sub.Currency,
            BillingPeriod = sub.BillingPeriod,
            PeriodStart = baseStart,
            PeriodEnd = newEnd,
            PaidAt = nowUtc,
            Source = PlatformSubscriptionPaymentSource.Manual,
            RecordedByPlatformAdminUserId = _currentUser.UserId,
            Notes = request.Notes?.Trim(),
        });

        sub.CurrentPeriodStart = baseStart;
        sub.CurrentPeriodEnd = newEnd;
        sub.Status = SubscriptionStatus.Active;
        sub.TrialEndsAt = null;            // trial sona erdi (ödeme yapıldı)
        sub.CancelAtPeriodEnd = false;     // ödeme bekleyen iptali temizler
        sub.CancelRequestedAt = null;
        if (usedIntro) sub.IntroPeriodsRemaining = sub.IntroPeriodsRemaining - 1;

        await _db.SaveChangesAsync(ct);
        return SubscriptionMapping.ToDto(sub, today);
    }
}

// ---- Abonelik tahsilat geçmişi (PlatformAdmin) ----
public sealed record GetSubscriptionPaymentsQuery(Guid StudioId) : IQuery<IReadOnlyList<SubscriptionPaymentDto>>;

public sealed class GetSubscriptionPaymentsHandler : IQueryHandler<GetSubscriptionPaymentsQuery, IReadOnlyList<SubscriptionPaymentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetSubscriptionPaymentsHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<SubscriptionPaymentDto>> Handle(GetSubscriptionPaymentsQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var rows = await _db.PlatformSubscriptionPayments.AsNoTracking()
            .Where(p => p.StudioId == request.StudioId)
            .OrderByDescending(p => p.PaidAt).Take(100).ToListAsync(ct);
        return rows.Select(SubscriptionPaymentMapping.ToDto).ToList();
    }
}
