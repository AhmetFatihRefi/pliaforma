using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Packages;

public sealed record PackagePlanDto(
    Guid Id, string Name, string? Description, int CreditCount, decimal Price, int? ValidityDays, bool IsActive,
    bool AllowAllClassTypes, IReadOnlyList<Guid> ClassTypeIds,
    decimal CurrentEffectivePrice, string CurrentEffectivePriceCurrency,
    decimal? NextEffectivePrice, DateOnly? NextEffectiveFrom);

public sealed record PackagePlanPriceVersionDto(
    Guid Id, decimal Price, string Currency, DateOnly EffectiveFrom, bool IsCurrent, DateTime CreatedAt);

/// <summary>Plan → DTO (fiyat versiyonu + ders grubu kapsamıyla). Effective fiyat versiyon tablosundan hesaplanır.</summary>
internal static class PackagePlanMapping
{
    public static PackagePlanDto ToDto(PackagePlan p, DateOnly today)
    {
        var current = PackagePricing.CurrentVersion(p.PriceVersions, today);
        var next = PackagePricing.NextVersion(p.PriceVersions, today);
        var classTypeIds = p.ClassTypes.Where(c => c.IsActive).Select(c => c.ClassTypeId).ToList();
        return new PackagePlanDto(
            p.Id, p.Name, p.Description, p.CreditCount, p.Price, p.ValidityDays, p.IsActive,
            p.AllowAllClassTypes, classTypeIds,
            current?.Price ?? p.Price, current?.Currency ?? "TRY",
            next?.Price, next?.EffectiveFrom);
    }
}

// ---- Liste ----
public sealed record GetPackagePlansQuery(bool OnlyActive = false) : IQuery<IReadOnlyList<PackagePlanDto>>;

public sealed class GetPackagePlansHandler : IQueryHandler<GetPackagePlansQuery, IReadOnlyList<PackagePlanDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetPackagePlansHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<PackagePlanDto>> Handle(GetPackagePlansQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var q = _db.PackagePlans.Where(p => p.StudioId == studioId);
        if (request.OnlyActive) q = q.Where(p => p.IsActive);
        var plans = await q.OrderBy(p => p.Name)
            .Include(p => p.PriceVersions).Include(p => p.ClassTypes)
            .ToListAsync(ct);
        return plans.Select(p => PackagePlanMapping.ToDto(p, today)).ToList();
    }
}

// ---- Tek plan (fiyat versiyonları + kapsam) ----
public sealed record GetPackagePlanQuery(Guid Id) : IQuery<PackagePlanDto>;

public sealed class GetPackagePlanHandler : IQueryHandler<GetPackagePlanQuery, PackagePlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetPackagePlanHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PackagePlanDto> Handle(GetPackagePlanQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var plan = await _db.PackagePlans.Where(p => p.Id == request.Id && p.StudioId == studioId)
            .Include(p => p.PriceVersions).Include(p => p.ClassTypes)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Paket planı", request.Id);
        return PackagePlanMapping.ToDto(plan, today);
    }
}

// ---- Fiyat versiyonları listesi ----
public sealed record GetPackagePlanPriceVersionsQuery(Guid PlanId) : IQuery<IReadOnlyList<PackagePlanPriceVersionDto>>;

public sealed class GetPackagePlanPriceVersionsHandler : IQueryHandler<GetPackagePlanPriceVersionsQuery, IReadOnlyList<PackagePlanPriceVersionDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetPackagePlanPriceVersionsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<PackagePlanPriceVersionDto>> Handle(GetPackagePlanPriceVersionsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var exists = await _db.PackagePlans.AnyAsync(p => p.Id == request.PlanId && p.StudioId == studioId, ct);
        if (!exists) throw new NotFoundException("Paket planı", request.PlanId);

        var versions = await _db.PackagePlanPriceVersions
            .Where(v => v.StudioId == studioId && v.PackagePlanId == request.PlanId)
            .OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.CreatedAt).ToListAsync(ct);
        var currentId = PackagePricing.CurrentVersion(versions, today)?.Id;
        return versions.Select(v => new PackagePlanPriceVersionDto(
            v.Id, v.Price, v.Currency, v.EffectiveFrom, v.Id == currentId, v.CreatedAt)).ToList();
    }
}

/// <summary>Ortak: plan kapsam (PackagePlanClassType) + fiyat cache senkronizasyonu.</summary>
internal static class PackagePlanWrite
{
    /// <summary>Belirtilen ClassType id'lerinin stüdyoya ait olduğunu doğrular.</summary>
    public static async Task ValidateClassTypesAsync(IAppDbContext db, Guid studioId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var distinct = ids.Distinct().ToList();
        var validCount = await db.ClassTypes.CountAsync(c => c.StudioId == studioId && distinct.Contains(c.Id), ct);
        if (validCount != distinct.Count)
            throw new BusinessRuleException("Seçilen ders gruplarından bazıları bu stüdyoya ait değil.");
    }

    /// <summary>Plan kapsamını verilen ClassType id'leriyle eşitler (soft-delete + ekle).</summary>
    public static async Task SyncClassTypesAsync(IAppDbContext db, Guid studioId, PackagePlan plan, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToHashSet();
        var existing = await db.PackagePlanClassTypes.Where(c => c.PackagePlanId == plan.Id).ToListAsync(ct);
        foreach (var row in existing.Where(r => !wanted.Contains(r.ClassTypeId)))
            db.PackagePlanClassTypes.Remove(row); // soft delete
        var have = existing.Select(r => r.ClassTypeId).ToHashSet();
        foreach (var id in wanted.Where(id => !have.Contains(id)))
            db.PackagePlanClassTypes.Add(new PackagePlanClassType { StudioId = studioId, PackagePlanId = plan.Id, ClassTypeId = id });
    }

    /// <summary>Plan.Price cache'ini bugünkü effective fiyata senkronlar (source of truth = versiyonlar).</summary>
    public static async Task SyncPriceCacheAsync(IAppDbContext db, Guid studioId, PackagePlan plan, DateOnly today, CancellationToken ct)
    {
        var versions = await db.PackagePlanPriceVersions.Where(v => v.StudioId == studioId && v.PackagePlanId == plan.Id).ToListAsync(ct);
        var cur = PackagePricing.CurrentVersion(versions, today);
        if (cur is not null) plan.Price = cur.Price;
    }

    public static async Task<string> StudioCurrencyAsync(IAppDbContext db, Guid studioId, CancellationToken ct)
    {
        var cur = await db.Studios.Where(s => s.Id == studioId).Select(s => s.Currency).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(cur) ? "TRY" : cur!;
    }
}

// ---- Oluştur ----
public sealed record CreatePackagePlanCommand(
    string Name, string? Description, int CreditCount, decimal Price, int? ValidityDays,
    bool AllowAllClassTypes = true, IReadOnlyList<Guid>? ClassTypeIds = null,
    decimal? FuturePrice = null, DateOnly? FutureEffectiveFrom = null)
    : ICommand<PackagePlanDto>, IStudioWriteRequest;

public sealed class CreatePackagePlanValidator : AbstractValidator<CreatePackagePlanCommand>
{
    public CreatePackagePlanValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CreditCount).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidityDays).GreaterThan(0).When(x => x.ValidityDays.HasValue);
        RuleFor(x => x.FuturePrice).GreaterThanOrEqualTo(0).When(x => x.FuturePrice.HasValue);
        RuleFor(x => x.FutureEffectiveFrom).NotNull().When(x => x.FuturePrice.HasValue)
            .WithMessage("Gelecek fiyat için geçerlilik tarihi zorunludur.");
    }
}

public sealed class CreatePackagePlanHandler : ICommandHandler<CreatePackagePlanCommand, PackagePlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public CreatePackagePlanHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PackagePlanDto> Handle(CreatePackagePlanCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var name = request.Name.Trim();
        if (await _db.PackagePlans.AnyAsync(p => p.StudioId == studioId && p.Name == name, ct))
            throw new ConflictException("Bu adda bir paket planı zaten var.");

        var classTypeIds = request.ClassTypeIds ?? [];
        await PackagePlanWrite.ValidateClassTypesAsync(_db, studioId, classTypeIds, ct);

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var currency = await PackagePlanWrite.StudioCurrencyAsync(_db, studioId, ct);

        var entity = new PackagePlan
        {
            StudioId = studioId,
            Name = name,
            Description = request.Description?.Trim(),
            CreditCount = request.CreditCount,
            Price = request.Price,
            ValidityDays = request.ValidityDays,
            AllowAllClassTypes = request.AllowAllClassTypes,
        };
        _db.PackagePlans.Add(entity);

        // Başlangıç fiyat versiyonu (bugünden geçerli) — fiyatın source of truth'u.
        _db.PackagePlanPriceVersions.Add(new PackagePlanPriceVersion
        {
            StudioId = studioId, PackagePlanId = entity.Id, Price = request.Price, Currency = currency,
            EffectiveFrom = today, CreatedByUserId = userId,
        });
        if (request.FuturePrice is { } fp && request.FutureEffectiveFrom is { } feff)
            _db.PackagePlanPriceVersions.Add(new PackagePlanPriceVersion
            {
                StudioId = studioId, PackagePlanId = entity.Id, Price = fp, Currency = currency,
                EffectiveFrom = feff, CreatedByUserId = userId,
            });

        if (!request.AllowAllClassTypes)
            foreach (var id in classTypeIds.Distinct())
                _db.PackagePlanClassTypes.Add(new PackagePlanClassType { StudioId = studioId, PackagePlanId = entity.Id, ClassTypeId = id });

        await _db.SaveChangesAsync(ct);

        var saved = await _db.PackagePlans.Where(p => p.Id == entity.Id)
            .Include(p => p.PriceVersions).Include(p => p.ClassTypes).FirstAsync(ct);
        return PackagePlanMapping.ToDto(saved, today);
    }
}

// ---- Güncelle ----
public sealed record UpdatePackagePlanCommand(
    Guid Id, string Name, string? Description, int CreditCount, decimal Price, int? ValidityDays, bool IsActive,
    bool AllowAllClassTypes = true, IReadOnlyList<Guid>? ClassTypeIds = null,
    decimal? NewPrice = null, DateOnly? NewPriceEffectiveFrom = null)
    : ICommand<PackagePlanDto>, IStudioWriteRequest;

public sealed class UpdatePackagePlanValidator : AbstractValidator<UpdatePackagePlanCommand>
{
    public UpdatePackagePlanValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CreditCount).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidityDays).GreaterThan(0).When(x => x.ValidityDays.HasValue);
        RuleFor(x => x.NewPrice).GreaterThanOrEqualTo(0).When(x => x.NewPrice.HasValue);
        RuleFor(x => x.NewPriceEffectiveFrom).NotNull().When(x => x.NewPrice.HasValue)
            .WithMessage("Yeni fiyat için geçerlilik tarihi zorunludur.");
    }
}

public sealed class UpdatePackagePlanHandler : ICommandHandler<UpdatePackagePlanCommand, PackagePlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public UpdatePackagePlanHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PackagePlanDto> Handle(UpdatePackagePlanCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var plan = await _db.PackagePlans.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Paket planı", request.Id);

        var name = request.Name.Trim();
        if (await _db.PackagePlans.AnyAsync(p => p.StudioId == studioId && p.Name == name && p.Id != plan.Id, ct))
            throw new ConflictException("Bu adda bir paket planı zaten var.");

        var classTypeIds = request.ClassTypeIds ?? [];
        await PackagePlanWrite.ValidateClassTypesAsync(_db, studioId, classTypeIds, ct);

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var currency = await PackagePlanWrite.StudioCurrencyAsync(_db, studioId, ct);

        plan.Name = name;
        plan.Description = request.Description?.Trim();
        plan.CreditCount = request.CreditCount;
        plan.ValidityDays = request.ValidityDays;
        plan.IsActive = request.IsActive;
        plan.AllowAllClassTypes = request.AllowAllClassTypes;

        await PackagePlanWrite.SyncClassTypesAsync(_db, studioId, plan, request.AllowAllClassTypes ? [] : classTypeIds, ct);

        // Bugünkü effective fiyatı request.Price'a çek (farklıysa bugün-geçerli versiyonu upsert et).
        var versions = await _db.PackagePlanPriceVersions.Where(v => v.StudioId == studioId && v.PackagePlanId == plan.Id).ToListAsync(ct);
        var currentEffective = PackagePricing.CurrentVersion(versions, today);
        if (currentEffective is null || currentEffective.Price != request.Price)
        {
            var todayVersion = versions.FirstOrDefault(v => v.EffectiveFrom == today);
            if (todayVersion is not null) todayVersion.Price = request.Price;
            else _db.PackagePlanPriceVersions.Add(new PackagePlanPriceVersion
            {
                StudioId = studioId, PackagePlanId = plan.Id, Price = request.Price, Currency = currency,
                EffectiveFrom = today, CreatedByUserId = userId,
            });
        }
        if (request.NewPrice is { } np && request.NewPriceEffectiveFrom is { } neff)
        {
            var atDate = versions.FirstOrDefault(v => v.EffectiveFrom == neff);
            if (atDate is not null) atDate.Price = np;
            else _db.PackagePlanPriceVersions.Add(new PackagePlanPriceVersion
            {
                StudioId = studioId, PackagePlanId = plan.Id, Price = np, Currency = currency,
                EffectiveFrom = neff, CreatedByUserId = userId,
            });
        }

        await PackagePlanWrite.SyncPriceCacheAsync(_db, studioId, plan, today, ct);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.PackagePlans.Where(p => p.Id == plan.Id)
            .Include(p => p.PriceVersions).Include(p => p.ClassTypes).FirstAsync(ct);
        return PackagePlanMapping.ToDto(saved, today);
    }
}

// ---- Fiyat versiyonu ekle ----
public sealed record AddPackagePlanPriceVersionCommand(Guid PlanId, decimal Price, DateOnly EffectiveFrom)
    : ICommand<PackagePlanPriceVersionDto>, IStudioWriteRequest;

public sealed class AddPackagePlanPriceVersionValidator : AbstractValidator<AddPackagePlanPriceVersionCommand>
{
    public AddPackagePlanPriceVersionValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddPackagePlanPriceVersionHandler : ICommandHandler<AddPackagePlanPriceVersionCommand, PackagePlanPriceVersionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public AddPackagePlanPriceVersionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<PackagePlanPriceVersionDto> Handle(AddPackagePlanPriceVersionCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var plan = await _db.PackagePlans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Paket planı", request.PlanId);

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var currency = await PackagePlanWrite.StudioCurrencyAsync(_db, studioId, ct);

        // Aynı tarihte versiyon varsa fiyatını güncelle, yoksa ekle.
        var existing = await _db.PackagePlanPriceVersions
            .FirstOrDefaultAsync(v => v.StudioId == studioId && v.PackagePlanId == plan.Id && v.EffectiveFrom == request.EffectiveFrom, ct);
        PackagePlanPriceVersion version;
        if (existing is not null) { existing.Price = request.Price; version = existing; }
        else
        {
            version = new PackagePlanPriceVersion
            {
                StudioId = studioId, PackagePlanId = plan.Id, Price = request.Price, Currency = currency,
                EffectiveFrom = request.EffectiveFrom, CreatedByUserId = userId,
            };
            _db.PackagePlanPriceVersions.Add(version);
        }

        await PackagePlanWrite.SyncPriceCacheAsync(_db, studioId, plan, today, ct);
        await _db.SaveChangesAsync(ct);

        var isCurrent = version.EffectiveFrom <= today &&
            !await _db.PackagePlanPriceVersions.AnyAsync(v => v.StudioId == studioId && v.PackagePlanId == plan.Id
                && v.EffectiveFrom <= today && v.EffectiveFrom > version.EffectiveFrom, ct);
        return new PackagePlanPriceVersionDto(version.Id, version.Price, version.Currency, version.EffectiveFrom, isCurrent, version.CreatedAt);
    }
}

// ---- Sil (soft delete) ----
public sealed record DeletePackagePlanCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class DeletePackagePlanHandler : ICommandHandler<DeletePackagePlanCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public DeletePackagePlanHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<Unit> Handle(DeletePackagePlanCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var plan = await _db.PackagePlans.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Paket planı", request.Id);
        _db.PackagePlans.Remove(plan); // AuditInterceptor → soft delete
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
