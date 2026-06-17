using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Instructors;

/// <summary>Bir hocanın atandığı pilates gruplarını listeler (aktif stüdyo kapsamında).</summary>
public sealed record GetInstructorClassTypesQuery(Guid InstructorProfileId)
    : IQuery<IReadOnlyList<InstructorAssignmentDto>>;

public sealed class GetInstructorClassTypesHandler
    : IQueryHandler<GetInstructorClassTypesQuery, IReadOnlyList<InstructorAssignmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetInstructorClassTypesHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InstructorAssignmentDto>> Handle(GetInstructorClassTypesQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var profileExists = await _db.InstructorProfiles
            .AnyAsync(p => p.Id == request.InstructorProfileId && p.StudioId == studioId, cancellationToken);
        if (!profileExists)
            throw new NotFoundException("Hoca profili", request.InstructorProfileId);

        return await _db.InstructorClassTypes
            .Where(x => x.StudioId == studioId && x.InstructorProfileId == request.InstructorProfileId)
            .OrderBy(x => x.ClassType!.Name)
            .Select(x => new InstructorAssignmentDto(x.Id, x.ClassTypeId, x.ClassType!.Name, x.RateOverride))
            .ToListAsync(cancellationToken);
    }
}
