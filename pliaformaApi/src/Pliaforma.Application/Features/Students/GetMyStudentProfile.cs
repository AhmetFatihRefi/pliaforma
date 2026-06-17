using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Öğrencinin kendi profilini döner (aktif stüdyo bağlamında UserId eşleşmesiyle). Öğrenci kendi
/// sağlık notunu görebilir. Profil yoksa 404.
/// </summary>
public sealed record GetMyStudentProfileQuery : IQuery<StudentDto>;

public sealed class GetMyStudentProfileHandler : IQueryHandler<GetMyStudentProfileQuery, StudentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyStudentProfileHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(GetMyStudentProfileQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .OrderByDescending(s => s.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bu stüdyoda öğrenci profiliniz bulunamadı.");

        return StudentDto.From(student, includeHealth: true);
    }
}
