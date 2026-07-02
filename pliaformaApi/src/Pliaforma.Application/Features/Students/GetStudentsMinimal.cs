using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Sprint 46 — Pilot Simple People Mode. Hoca için minimal öğrenci listesi/araması: yalnız Id + FullName +
/// hesap-var-mı. Sağlık/finans/sözleşme/telefon/e-posta gibi hassas alanlar DÖNMEZ. Owner/StudioAdmin her zaman;
/// Instructor YALNIZ pilot mod açıkken (aksi halde 403) ve TÜM stüdyo öğrencilerini (Inactive hariç) görür —
/// grup derslerinde ortak öğrenci pratikliği için (v1.1.2 kilidi tam-DTO listesinde korunur). Tenant-scoped.
/// </summary>
public sealed record StudentMinimalDto(Guid Id, string FullName, bool HasAccount);

// Sprint 48: öğrenci listesi okuması → Removed/rol-düşürülen üye access token'ıyla okuyamaz.
public sealed record GetStudentsMinimalQuery(string? Search = null) : IQuery<IReadOnlyList<StudentMinimalDto>>, IStudioReadRequest;

public sealed class GetStudentsMinimalHandler : IQueryHandler<GetStudentsMinimalQuery, IReadOnlyList<StudentMinimalDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPilotModeInfo _pilotMode;

    public GetStudentsMinimalHandler(IAppDbContext db, ICurrentUserService currentUser, IPilotModeInfo pilotMode)
    {
        _db = db;
        _currentUser = currentUser;
        _pilotMode = pilotMode;
    }

    public async Task<IReadOnlyList<StudentMinimalDto>> Handle(GetStudentsMinimalQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var role = _currentUser.StudioRole;

        if (role is StudioRole.Owner or StudioRole.StudioAdmin)
        {
            // erişim açık
        }
        else if (role == StudioRole.Instructor)
        {
            if (!_pilotMode.SimplePeopleModeEnabled)
                throw new ForbiddenException("Öğrenci listesine erişim yetkiniz yok.");
        }
        else
        {
            throw new ForbiddenException("Öğrenci listesine erişim yetkiniz yok.");
        }

        var query = _db.StudentProfiles.AsNoTracking()
            .Where(s => s.StudioId == studioId && s.Status != StudentStatus.Inactive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s => s.FullName.ToLower().Contains(term));
        }

        return await query
            .OrderBy(s => s.FullName)
            .Select(s => new StudentMinimalDto(s.Id, s.FullName, s.UserId != null))
            .ToListAsync(ct);
    }
}
