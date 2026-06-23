using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
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
    private readonly ISecurityAuditLogger _audit;

    public GetMyStudentProfileHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
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

        // Denetim (31C-1): öğrenci KENDİ sağlık notunu görüyor (actor = öğrencinin kendisi); yalnız dolu ise.
        if (!string.IsNullOrWhiteSpace(student.HealthNotes))
            await _audit.LogAsync(SecurityAuditEventType.HealthNotesAccessed, SecurityAuditOutcome.Success,
                actorUserId: userId, studioId: studioId,
                targetType: "StudentProfile", targetId: student.Id, detail: "count=1; self",
                cancellationToken: cancellationToken);

        return StudentDto.From(student, includeHealth: true);
    }
}
