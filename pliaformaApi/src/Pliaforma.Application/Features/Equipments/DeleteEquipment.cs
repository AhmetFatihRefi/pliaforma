using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>
/// Sprint 32A: Bir ekipman havuzunu siler (soft-delete). Restrict: bağlı ClassType varsa silinemez (409) →
/// önce havuzu pasifleştir veya ClassType bağlantısını kaldır.
/// </summary>
public sealed record DeleteEquipmentCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class DeleteEquipmentHandler : ICommandHandler<DeleteEquipmentCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteEquipmentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteEquipmentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ekipman", request.Id);

        // Restrict: bağlı ders tipi varsa silmeyi engelle (FK Restrict'i uygulama katmanında da net mesajla doğrula).
        var boundCount = await _db.ClassTypes
            .CountAsync(c => c.StudioId == studioId && c.EquipmentId == entity.Id, cancellationToken);
        if (boundCount > 0)
            throw new ConflictException(
                $"Bu ekipman {boundCount} ders tipine bağlı; silinemez. Önce ders tipi bağlantılarını kaldırın veya ekipmanı pasifleştirin.");

        _db.Equipment.Remove(entity); // AuditInterceptor soft-delete'e çevirir
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
