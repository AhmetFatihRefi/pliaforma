using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Packages;

public sealed record PackagePlanDto(
    Guid Id, string Name, string? Description, int CreditCount, decimal Price, int? ValidityDays, bool IsActive)
{
    public static PackagePlanDto From(PackagePlan p) =>
        new(p.Id, p.Name, p.Description, p.CreditCount, p.Price, p.ValidityDays, p.IsActive);
}

// ---- Liste ----
public sealed record GetPackagePlansQuery(bool OnlyActive = false) : IQuery<IReadOnlyList<PackagePlanDto>>;

public sealed class GetPackagePlansHandler : IQueryHandler<GetPackagePlansQuery, IReadOnlyList<PackagePlanDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetPackagePlansHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<PackagePlanDto>> Handle(GetPackagePlansQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var q = _db.PackagePlans.Where(p => p.StudioId == studioId);
        if (request.OnlyActive) q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.Name).Select(p => PackagePlanDto.From(p)).ToListAsync(ct);
    }
}

// ---- Oluştur ----
public sealed record CreatePackagePlanCommand(string Name, string? Description, int CreditCount, decimal Price, int? ValidityDays)
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
    }
}

public sealed class CreatePackagePlanHandler : ICommandHandler<CreatePackagePlanCommand, PackagePlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public CreatePackagePlanHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<PackagePlanDto> Handle(CreatePackagePlanCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var name = request.Name.Trim();
        if (await _db.PackagePlans.AnyAsync(p => p.StudioId == studioId && p.Name == name, ct))
            throw new ConflictException("Bu adda bir paket planı zaten var.");

        var entity = new PackagePlan
        {
            StudioId = studioId,
            Name = name,
            Description = request.Description?.Trim(),
            CreditCount = request.CreditCount,
            Price = request.Price,
            ValidityDays = request.ValidityDays,
        };
        _db.PackagePlans.Add(entity);
        await _db.SaveChangesAsync(ct);
        return PackagePlanDto.From(entity);
    }
}

// ---- Güncelle ----
public sealed record UpdatePackagePlanCommand(Guid Id, string Name, string? Description, int CreditCount, decimal Price, int? ValidityDays, bool IsActive)
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
    }
}

public sealed class UpdatePackagePlanHandler : ICommandHandler<UpdatePackagePlanCommand, PackagePlanDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public UpdatePackagePlanHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<PackagePlanDto> Handle(UpdatePackagePlanCommand request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var plan = await _db.PackagePlans.FirstOrDefaultAsync(p => p.Id == request.Id && p.StudioId == studioId, ct)
            ?? throw new NotFoundException("Paket planı", request.Id);

        var name = request.Name.Trim();
        if (await _db.PackagePlans.AnyAsync(p => p.StudioId == studioId && p.Name == name && p.Id != plan.Id, ct))
            throw new ConflictException("Bu adda bir paket planı zaten var.");

        plan.Name = name;
        plan.Description = request.Description?.Trim();
        plan.CreditCount = request.CreditCount;
        plan.Price = request.Price;
        plan.ValidityDays = request.ValidityDays;
        plan.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return PackagePlanDto.From(plan);
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
