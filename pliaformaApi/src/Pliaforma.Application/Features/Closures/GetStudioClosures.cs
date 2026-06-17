using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Closures;

// ---- Liste ----
public sealed record GetStudioClosuresQuery : IQuery<IReadOnlyList<StudioClosureDto>>;

public sealed class GetStudioClosuresHandler : IQueryHandler<GetStudioClosuresQuery, IReadOnlyList<StudioClosureDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudioClosuresHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<StudioClosureDto>> Handle(GetStudioClosuresQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        return await _db.StudioClosures
            .Where(c => c.StudioId == studioId)
            .OrderByDescending(c => c.StartAt)
            .Select(c => StudioClosureDto.From(c))
            .ToListAsync(cancellationToken);
    }
}

// ---- Tekil ----
public sealed record GetStudioClosureQuery(Guid Id) : IQuery<StudioClosureDto>;

public sealed class GetStudioClosureHandler : IQueryHandler<GetStudioClosureQuery, StudioClosureDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudioClosureHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudioClosureDto> Handle(GetStudioClosureQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var closure = await _db.StudioClosures
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Kapanış kaydı", request.Id);
        return StudioClosureDto.From(closure);
    }
}
