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
    int DefaultDurationMinutes,
    string? Description,
    bool IsActive,
    // Sprint 32A: opsiyonel ekipman havuzu bağı (null → bağı kaldırır).
    Guid? EquipmentId = null) : ICommand<ClassTypeDto>, IStudioWriteRequest;

public sealed class UpdateClassTypeValidator : AbstractValidator<UpdateClassTypeCommand>
{
    public UpdateClassTypeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(10, 240);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateClassTypeHandler : ICommandHandler<UpdateClassTypeCommand, ClassTypeDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public UpdateClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ClassTypeDto> Handle(UpdateClassTypeCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        // Tenant izolasyonu: yalnızca aktif stüdyonun kaydı bulunur (başka stüdyonunki → NotFound).
        var entity = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.Id);

        // Sprint 32A: ekipman havuzu verildiyse aktif stüdyoya ait olmalı (tenant izolasyonu).
        var equipmentName = await ClassTypeEquipmentResolver.ResolveNameAsync(_db, studioId, request.EquipmentId, cancellationToken);

        // Sprint 32B (legacy 9999, A seçeneği): null→non-null veya farklı havuza geçişte, gelecekte planlanmış
        // sınırsız kapasiteli seans varsa bağlamayı engelle (önce o seanslara sonlu kapasite atanmalı).
        if (request.EquipmentId is { } newEquipmentId && newEquipmentId != entity.EquipmentId)
            await ClassTypeEquipmentResolver.EnsureNoUnlimitedFutureSessionsAsync(_db, _clock, studioId, entity.Id, cancellationToken);

        // Sprint 28A: yalnız ad/süre/açıklama/aktiflik güncellenir; dormant alanlara (Equipment enum/CapacityType/
        // MaxCapacity/Level/Purpose) dokunulmaz. Sprint 32A: EquipmentId bağı güncellenir.
        entity.Name = request.Name.Trim();
        entity.DefaultDurationMinutes = request.DefaultDurationMinutes;
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;
        entity.EquipmentId = request.EquipmentId;

        await _db.SaveChangesAsync(cancellationToken);
        return ClassTypeDto.From(entity, equipmentName);
    }
}
