using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;

namespace Pliaforma.Application.Features.Packages;

/// <summary>Öğrenci kendi paketlerini (kalan hak + geçerlilik + effective status) görür.</summary>
public sealed record GetMyPackagesQuery : IQuery<IReadOnlyList<StudentPackageDto>>;

public sealed class GetMyPackagesHandler : IQueryHandler<GetMyPackagesQuery, IReadOnlyList<StudentPackageDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetMyPackagesHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock) { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<StudentPackageDto>> Handle(GetMyPackagesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        if (profileId is null)
            return [];

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        return await PackageRules.Project(
            _db.StudentPackages.Where(p => p.StudioId == studioId && p.StudentProfileId == profileId)
                .OrderByDescending(p => p.CreatedAt), today).ToListAsync(ct);
    }
}
