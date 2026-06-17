using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.ClassTypes;

/// <summary>Mevcut bir pilates grubunu günceller. Yalnızca aktif stüdyonun grubu güncellenebilir.</summary>
public sealed record UpdateClassTypeCommand(
    Guid Id,
    string Name,
    EquipmentType Equipment,
    ClassCapacityType CapacityType,
    int MaxCapacity,
    ClassLevel Level,
    ClassPurpose Purpose,
    int DefaultDurationMinutes,
    string? Description,
    bool IsActive) : ICommand<ClassTypeDto>, IStudioWriteRequest;

public sealed class UpdateClassTypeValidator : AbstractValidator<UpdateClassTypeCommand>
{
    public UpdateClassTypeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(10, 240);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.MaxCapacity)
            .Must((cmd, cap) => CapacityRules.IsValid(cmd.CapacityType, cap))
            .WithMessage(CapacityRules.Message);
    }
}

public sealed class UpdateClassTypeHandler : ICommandHandler<UpdateClassTypeCommand, ClassTypeDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClassTypeDto> Handle(UpdateClassTypeCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        // Tenant izolasyonu: yalnızca aktif stüdyonun kaydı bulunur (başka stüdyonunki → NotFound).
        var entity = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.Id);

        entity.Name = request.Name.Trim();
        entity.Equipment = request.Equipment;
        entity.CapacityType = request.CapacityType;
        entity.MaxCapacity = request.MaxCapacity;
        entity.Level = request.Level;
        entity.Purpose = request.Purpose;
        entity.DefaultDurationMinutes = request.DefaultDurationMinutes;
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return ClassTypeDto.From(entity);
    }
}
