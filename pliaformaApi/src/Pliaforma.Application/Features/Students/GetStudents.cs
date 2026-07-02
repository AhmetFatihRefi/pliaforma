using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Aktif stüdyonun öğrencilerini listeler/arar. Owner/StudioAdmin tümünü; Instructor yalnızca KENDİ
/// oluşturduklarını görür; Student bu uca erişemez. Soft-delete'ler global filter ile gizli.
/// </summary>
// Sprint 48: hassas PII/sağlık okuması → Removed/rol-düşürülen üye access token'ıyla okuyamaz.
public sealed record GetStudentsQuery(string? Search = null, StudentLevel? Level = null) : IQuery<IReadOnlyList<StudentDto>>, IStudioReadRequest;

public sealed class GetStudentsHandler : IQueryHandler<GetStudentsQuery, IReadOnlyList<StudentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;

    public GetStudentsHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<StudentDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var role = _currentUser.StudioRole;

        var query = _db.StudentProfiles.Where(s => s.StudioId == studioId);

        if (role is StudioRole.Owner or StudioRole.StudioAdmin)
        {
            // tümü
        }
        else if (role == StudioRole.Instructor)
        {
            var userId = _currentUser.RequireUserId();
            query = query.Where(s => s.CreatedByUserId == userId);
        }
        else
        {
            throw new ForbiddenException("Öğrenci listesine erişim yetkiniz yok.");
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Sağlayıcı-bağımsız case-insensitive arama (EF → lower(...) LIKE).
            var term = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.FullName.ToLower().Contains(term)
                || (s.Phone != null && s.Phone.ToLower().Contains(term))
                || (s.Email != null && s.Email.ToLower().Contains(term)));
        }

        // Sprint 28C-1: opsiyonel seviye filtresi (öğrenci-seçim kolaylığı; null → tümü). ClassType.Level kullanılmaz.
        if (request.Level is { } level)
            query = query.Where(s => s.Level == level);

        var list = await query.OrderBy(s => s.FullName).ToListAsync(cancellationToken);

        // Denetim (31C-1): toplu öğrenci verisi okundu — istek-başı TEK event, yalnız sayı (PII yok).
        await _audit.LogAsync(SecurityAuditEventType.StudentDataListed, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId,
            targetType: "StudentProfile", detail: $"count={list.Count}", cancellationToken: cancellationToken);

        // Listeye dönen tüm satırlar yönetilebilir olduğundan sağlık notu görünür.
        // Sağlık notu GERÇEKTEN dolu olan kayıt varsa tek HealthNotesAccessed (boşlar gürültü yaratmaz).
        var healthCount = list.Count(s => !string.IsNullOrWhiteSpace(s.HealthNotes));
        if (healthCount > 0)
            await _audit.LogAsync(SecurityAuditEventType.HealthNotesAccessed, SecurityAuditOutcome.Success,
                actorUserId: _currentUser.UserId, studioId: studioId,
                targetType: "StudentProfile", detail: $"count={healthCount}", cancellationToken: cancellationToken);

        return list.Select(s => StudentDto.From(s, includeHealth: true)).ToList();
    }
}
