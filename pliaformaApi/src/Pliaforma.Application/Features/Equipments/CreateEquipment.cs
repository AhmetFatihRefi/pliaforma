using FluentValidation;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>Sprint 32A: Aktif stüdyoda yeni bir ekipman havuzu oluşturur (enforcement yok; yalnız veri).</summary>
public sealed record CreateEquipmentCommand(
    string Name,
    string? Description,
    int Quantity,
    int PerUnitConcurrentUsage,
    bool IsActive = true) : ICommand<EquipmentDto>, IStudioWriteRequest;

public sealed class CreateEquipmentValidator : AbstractValidator<CreateEquipmentCommand>
{
    public CreateEquipmentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Adet 0 veya daha büyük olmalıdır.");
        RuleFor(x => x.PerUnitConcurrentUsage)
            .GreaterThanOrEqualTo(1).WithMessage("Ekipman başı eşzamanlı kullanım en az 1 olmalıdır.");
    }
}

public sealed class CreateEquipmentHandler : ICommandHandler<CreateEquipmentCommand, EquipmentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateEquipmentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EquipmentDto> Handle(CreateEquipmentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = new Equipment
        {
            StudioId = studioId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Quantity = request.Quantity,
            PerUnitConcurrentUsage = request.PerUnitConcurrentUsage,
            IsActive = request.IsActive,
        };

        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return EquipmentDto.From(entity, boundClassTypeCount: 0);
    }
}
