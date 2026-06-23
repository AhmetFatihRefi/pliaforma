using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.ClassTypes;

/// <summary>Aktif stüdyonun pilates gruplarını listeler.</summary>
public sealed record GetClassTypesQuery(bool? OnlyActive = null) : IQuery<IReadOnlyList<ClassTypeDto>>;

public sealed class GetClassTypesHandler : IQueryHandler<GetClassTypesQuery, IReadOnlyList<ClassTypeDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetClassTypesHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ClassTypeDto>> Handle(GetClassTypesQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var query = _db.ClassTypes.Where(c => c.StudioId == studioId);
        if (request.OnlyActive == true)
            query = query.Where(c => c.IsActive);

        // Sprint 32A: bağlı ekipman adını korelasyonlu alt sorguyla getir (EquipmentId null → null).
        return await query
            .OrderBy(c => c.Name)
            .Select(c => new ClassTypeDto(
                c.Id, c.Name, c.DefaultDurationMinutes, c.Description, c.IsActive,
                c.EquipmentId,
                _db.Equipment.Where(e => e.Id == c.EquipmentId).Select(e => e.Name).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Tek bir pilates grubunu getirir (aktif stüdyo kapsamında).</summary>
public sealed record GetClassTypeQuery(Guid Id) : IQuery<ClassTypeDto>;

public sealed class GetClassTypeHandler : IQueryHandler<GetClassTypeQuery, ClassTypeDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClassTypeDto> Handle(GetClassTypeQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.Id);

        var equipmentName = entity.EquipmentId is { } eid
            ? await _db.Equipment.Where(e => e.Id == eid && e.StudioId == studioId)
                .Select(e => e.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return ClassTypeDto.From(entity, equipmentName);
    }
}
