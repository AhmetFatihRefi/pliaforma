using FluentValidation;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.ClassTypes;

/// <summary>Aktif stüdyoda yeni bir pilates grubu (ClassType) oluşturur.</summary>
public sealed record CreateClassTypeCommand(
    string Name,
    EquipmentType Equipment,
    ClassCapacityType CapacityType,
    int MaxCapacity,
    ClassLevel Level,
    ClassPurpose Purpose,
    int DefaultDurationMinutes,
    string? Description) : ICommand<ClassTypeDto>, IStudioWriteRequest;

public sealed class CreateClassTypeValidator : AbstractValidator<CreateClassTypeCommand>
{
    public CreateClassTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(10, 240);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.MaxCapacity)
            .Must((cmd, cap) => CapacityRules.IsValid(cmd.CapacityType, cap))
            .WithMessage(CapacityRules.Message);
    }
}

public sealed class CreateClassTypeHandler : ICommandHandler<CreateClassTypeCommand, ClassTypeDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClassTypeDto> Handle(CreateClassTypeCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = new ClassType
        {
            StudioId = studioId,
            Name = request.Name.Trim(),
            Equipment = request.Equipment,
            CapacityType = request.CapacityType,
            MaxCapacity = request.MaxCapacity,
            Level = request.Level,
            Purpose = request.Purpose,
            DefaultDurationMinutes = request.DefaultDurationMinutes,
            Description = request.Description?.Trim(),
            IsActive = true,
        };

        _db.ClassTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ClassTypeDto.From(entity);
    }
}
