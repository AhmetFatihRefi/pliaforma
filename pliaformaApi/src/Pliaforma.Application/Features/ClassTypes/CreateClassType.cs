using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.ClassTypes;

/// <summary>Aktif stüdyoda yeni bir pilates grubu (ClassType) oluşturur. Sprint 28A: yalnız ad + süre + açıklama
/// (kapasite/seviye/amaç/ekipman enum'u kaldırıldı; dormant kolonlar nötr varsayılanla doldurulur).
/// Sprint 32A: opsiyonel ekipman havuzu bağı (EquipmentId).</summary>
public sealed record CreateClassTypeCommand(
    string Name,
    int DefaultDurationMinutes,
    string? Description,
    Guid? EquipmentId = null) : ICommand<ClassTypeDto>, IStudioWriteRequest;

public sealed class CreateClassTypeValidator : AbstractValidator<CreateClassTypeCommand>
{
    public CreateClassTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(10, 240);
        RuleFor(x => x.Description).MaximumLength(1000);
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

        // Sprint 32A: ekipman havuzu verildiyse aktif stüdyoya ait olmalı (tenant izolasyonu).
        var equipmentName = await ClassTypeEquipmentResolver.ResolveNameAsync(_db, studioId, request.EquipmentId, cancellationToken);

        var entity = new ClassType
        {
            StudioId = studioId,
            Name = request.Name.Trim(),
            DefaultDurationMinutes = request.DefaultDurationMinutes,
            Description = request.Description?.Trim(),
            IsActive = true,
            EquipmentId = request.EquipmentId,
            // Sprint 28A: dormant alanlar (UI/DTO'dan kaldırıldı) — nötr varsayılan; hiçbir mantık okumaz.
            Equipment = EquipmentType.Mat,
            CapacityType = ClassCapacityType.Group,
            MaxCapacity = 0,
            Level = ClassLevel.AllLevels,
            Purpose = ClassPurpose.General,
        };

        _db.ClassTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ClassTypeDto.From(entity, equipmentName);
    }
}
