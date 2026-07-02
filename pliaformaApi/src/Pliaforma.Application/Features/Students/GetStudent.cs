using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>Tek bir öğrenciyi getirir (tenant + yetki kontrolü). Sağlık notu yetkiye göre döner.</summary>
// Sprint 48: hassas PII/sağlık okuması → Removed/rol-düşürülen üye access token'ıyla okuyamaz.
public sealed record GetStudentQuery(Guid Id) : IQuery<StudentDto>, IStudioReadRequest;

public sealed class GetStudentHandler : IQueryHandler<GetStudentQuery, StudentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;

    public GetStudentHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<StudentDto> Handle(GetStudentQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.Id);

        StudentAccess.EnsureCanManage(_currentUser, student);

        var canSeeHealth = StudentAccess.CanSeeHealth(_currentUser, student);

        // Denetim (31C-1): HealthNotes YALNIZ yetki var + alan GERÇEKTEN dolu ise (boş = audit yok).
        if (canSeeHealth && !string.IsNullOrWhiteSpace(student.HealthNotes))
            await _audit.LogAsync(SecurityAuditEventType.HealthNotesAccessed, SecurityAuditOutcome.Success,
                actorUserId: _currentUser.UserId, studioId: studioId,
                targetType: "StudentProfile", targetId: student.Id, detail: "count=1",
                cancellationToken: cancellationToken);

        return StudentDto.From(student, includeHealth: canSeeHealth);
    }
}
