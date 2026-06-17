using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Müsaitlikleri listeler. Owner/StudioAdmin tümünü (opsiyonel instructorProfileId filtresiyle);
/// Instructor yalnızca kendi profilininkini görür.
/// </summary>
public sealed record GetAvailabilitiesQuery(Guid? InstructorProfileId = null)
    : IQuery<IReadOnlyList<InstructorAvailabilityDto>>;

public sealed class GetAvailabilitiesHandler
    : IQueryHandler<GetAvailabilitiesQuery, IReadOnlyList<InstructorAvailabilityDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAvailabilitiesHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InstructorAvailabilityDto>> Handle(GetAvailabilitiesQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);

        var query = _db.InstructorAvailabilities.Where(a => a.StudioId == studioId);

        if (ownProfileId is { } own)
            query = query.Where(a => a.InstructorProfileId == own); // Instructor → yalnızca kendi
        else if (request.InstructorProfileId is { } filter)
            query = query.Where(a => a.InstructorProfileId == filter);

        return await query
            .OrderBy(a => a.InstructorProfileId).ThenBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .Select(a => InstructorAvailabilityDto.From(a))
            .ToListAsync(cancellationToken);
    }
}
