using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>Sprint 32A: Mevcut bir ekipman havuzunu günceller (yalnız aktif stüdyonun kaydı).</summary>
public sealed record UpdateEquipmentCommand(
    Guid Id,
    string Name,
    string? Description,
    int Quantity,
    int PerUnitConcurrentUsage,
    bool IsActive) : ICommand<EquipmentDto>, IStudioWriteRequest;

public sealed class UpdateEquipmentValidator : AbstractValidator<UpdateEquipmentCommand>
{
    public UpdateEquipmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Adet 0 veya daha büyük olmalıdır.");
        RuleFor(x => x.PerUnitConcurrentUsage)
            .GreaterThanOrEqualTo(1).WithMessage("Ekipman başı eşzamanlı kullanım en az 1 olmalıdır.");
    }
}

public sealed class UpdateEquipmentHandler : ICommandHandler<UpdateEquipmentCommand, EquipmentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateEquipmentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EquipmentDto> Handle(UpdateEquipmentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ekipman", request.Id);

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.Quantity = request.Quantity;
        entity.PerUnitConcurrentUsage = request.PerUnitConcurrentUsage;
        entity.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        var boundCount = await _db.ClassTypes
            .CountAsync(c => c.StudioId == studioId && c.EquipmentId == entity.Id, cancellationToken);
        return EquipmentDto.From(entity, boundCount);
    }
}
