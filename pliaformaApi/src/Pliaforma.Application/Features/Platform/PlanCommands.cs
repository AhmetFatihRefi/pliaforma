using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Platform;

internal static class PlanMapping
{
    public static SubscriptionPlanDto ToDto(SubscriptionPlan p) => new(
        p.Id, p.Name, p.Tier, p.MonthlyPrice, p.Currency, p.BillingPeriod, p.IntroMonthlyPrice,
        p.IntroPeriodCount, p.IsCustomQuote, p.IsDefault, p.IsActive, p.DisplayOrder, p.Description,
        p.FeaturesJson, p.MaxInstructors, p.MaxStudents);
}

// ---- Plan listele ----
public sealed record GetSubscriptionPlansQuery(bool OnlyActive = false) : IQuery<IReadOnlyList<SubscriptionPlanDto>>;

public sealed class GetSubscriptionPlansHandler : IQueryHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetSubscriptionPlansHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(GetSubscriptionPlansQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var q = _db.SubscriptionPlans.AsNoTracking().AsQueryable();
        if (request.OnlyActive) q = q.Where(p => p.IsActive);
        var plans = await q.OrderBy(p => p.DisplayOrder).ThenBy(p => p.MonthlyPrice).ToListAsync(ct);
        return plans.Select(PlanMapping.ToDto).ToList();
    }
}

// ---- Plan oluştur / güncelle (ortak gövde) ----
public sealed record SubscriptionPlanInput(
    string Name, string? Tier, decimal MonthlyPrice, string Currency, BillingPeriod BillingPeriod,
    decimal? IntroMonthlyPrice, int? IntroPeriodCount, bool IsCustomQuote, bool IsDefault, bool IsActive,
    int DisplayOrder, string? Description, string? FeaturesJson, int? MaxInstructors, int? MaxStudents);

public sealed class SubscriptionPlanInputValidator : AbstractValidator<SubscriptionPlanInput>
{
    public SubscriptionPlanInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Tier).MaximumLength(50);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.IntroMonthlyPrice).GreaterThanOrEqualTo(0).When(x => x.IntroMonthlyPrice.HasValue);
        RuleFor(x => x.IntroPeriodCount).GreaterThan(0).When(x => x.IntroPeriodCount.HasValue);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.FeaturesJson).MaximumLength(4000);
        RuleFor(x => x.MaxInstructors).GreaterThanOrEqualTo(0).When(x => x.MaxInstructors.HasValue);
        RuleFor(x => x.MaxStudents).GreaterThanOrEqualTo(0).When(x => x.MaxStudents.HasValue);
    }
}

public sealed record CreateSubscriptionPlanCommand(SubscriptionPlanInput Input) : ICommand<SubscriptionPlanDto>;
public sealed class CreateSubscriptionPlanValidator : AbstractValidator<CreateSubscriptionPlanCommand>
{
    public CreateSubscriptionPlanValidator() => RuleFor(x => x.Input).SetValidator(new SubscriptionPlanInputValidator());
}

public sealed class CreateSubscriptionPlanHandler : ICommandHandler<CreateSubscriptionPlanCommand, SubscriptionPlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public CreateSubscriptionPlanHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<SubscriptionPlanDto> Handle(CreateSubscriptionPlanCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var i = request.Input;
        var plan = new SubscriptionPlan();
        Apply(plan, i);
        if (i.IsDefault && i.IsActive) await UnsetOtherDefaultsAsync(_db, plan.Id, ct);
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return PlanMapping.ToDto(plan);
    }

    internal static void Apply(SubscriptionPlan p, SubscriptionPlanInput i)
    {
        p.Name = i.Name.Trim();
        p.Tier = i.Tier?.Trim();
        p.MonthlyPrice = i.MonthlyPrice;
        p.Currency = i.Currency.Trim().ToUpperInvariant();
        p.BillingPeriod = i.BillingPeriod;
        p.IntroMonthlyPrice = i.IntroMonthlyPrice;
        p.IntroPeriodCount = i.IntroPeriodCount;
        p.IsCustomQuote = i.IsCustomQuote;
        p.IsDefault = i.IsDefault;
        p.IsActive = i.IsActive;
        p.DisplayOrder = i.DisplayOrder;
        p.Description = i.Description?.Trim();
        p.FeaturesJson = i.FeaturesJson;
        p.MaxInstructors = i.MaxInstructors;
        p.MaxStudents = i.MaxStudents;
    }

    /// <summary>Aynı anda tek aktif default plan: bu plan default olunca diğerlerinin default'unu kaldırır.</summary>
    internal static async Task UnsetOtherDefaultsAsync(IAppDbContext db, Guid keepId, CancellationToken ct)
    {
        var others = await db.SubscriptionPlans.Where(p => p.IsDefault && p.Id != keepId).ToListAsync(ct);
        foreach (var o in others) o.IsDefault = false;
    }
}

// ---- Plan güncelle ----
public sealed record UpdateSubscriptionPlanCommand(Guid Id, SubscriptionPlanInput Input) : ICommand<SubscriptionPlanDto>;
public sealed class UpdateSubscriptionPlanValidator : AbstractValidator<UpdateSubscriptionPlanCommand>
{
    public UpdateSubscriptionPlanValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).SetValidator(new SubscriptionPlanInputValidator());
    }
}

public sealed class UpdateSubscriptionPlanHandler : ICommandHandler<UpdateSubscriptionPlanCommand, SubscriptionPlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public UpdateSubscriptionPlanHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<SubscriptionPlanDto> Handle(UpdateSubscriptionPlanCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException("Plan", request.Id);
        CreateSubscriptionPlanHandler.Apply(plan, request.Input);
        if (plan.IsDefault && plan.IsActive)
            await CreateSubscriptionPlanHandler.UnsetOtherDefaultsAsync(_db, plan.Id, ct);
        await _db.SaveChangesAsync(ct);
        return PlanMapping.ToDto(plan);
    }
}

// ---- Plan sil (soft-delete) ----
public sealed record DeleteSubscriptionPlanCommand(Guid Id) : ICommand<Unit>;

public sealed class DeleteSubscriptionPlanHandler : ICommandHandler<DeleteSubscriptionPlanCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public DeleteSubscriptionPlanHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<Unit> Handle(DeleteSubscriptionPlanCommand request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException("Plan", request.Id);
        _db.SubscriptionPlans.Remove(plan); // AuditInterceptor → soft-delete
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
