using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>Aktif stüdyonun ekipman havuzlarını listeler (her biri için bağlı ders tipi sayısıyla).</summary>
public sealed record GetEquipmentListQuery(bool? OnlyActive = null) : IQuery<IReadOnlyList<EquipmentDto>>;

public sealed class GetEquipmentListHandler : IQueryHandler<GetEquipmentListQuery, IReadOnlyList<EquipmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEquipmentListHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EquipmentDto>> Handle(GetEquipmentListQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var query = _db.Equipment.Where(e => e.StudioId == studioId);
        if (request.OnlyActive == true)
            query = query.Where(e => e.IsActive);

        return await query
            .OrderBy(e => e.Name)
            .Select(e => new EquipmentDto(
                e.Id, e.Name, e.Description, e.Quantity, e.PerUnitConcurrentUsage,
                e.Quantity * e.PerUnitConcurrentUsage, e.IsActive,
                _db.ClassTypes.Count(c => c.StudioId == studioId && c.EquipmentId == e.Id)))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Tek ekipman + bağlı ders tipleri (aktif stüdyo kapsamında).</summary>
public sealed record GetEquipmentQuery(Guid Id) : IQuery<EquipmentDetailDto>;

public sealed class GetEquipmentHandler : IQueryHandler<GetEquipmentQuery, EquipmentDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEquipmentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<EquipmentDetailDto> Handle(GetEquipmentQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ekipman", request.Id);

        var bound = await _db.ClassTypes
            .Where(c => c.StudioId == studioId && c.EquipmentId == entity.Id)
            .OrderBy(c => c.Name)
            .Select(c => new BoundClassTypeDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);

        return new EquipmentDetailDto(
            entity.Id, entity.Name, entity.Description, entity.Quantity, entity.PerUnitConcurrentUsage,
            entity.Quantity * entity.PerUnitConcurrentUsage, entity.IsActive, bound);
    }
}
