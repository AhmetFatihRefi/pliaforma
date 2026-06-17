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
public sealed record GetStudentsQuery(string? Search = null) : IQuery<IReadOnlyList<StudentDto>>;

public sealed class GetStudentsHandler : IQueryHandler<GetStudentsQuery, IReadOnlyList<StudentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudentsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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

        var list = await query.OrderBy(s => s.FullName).ToListAsync(cancellationToken);

        // Listeye dönen tüm satırlar yönetilebilir olduğundan sağlık notu görünür.
        return list.Select(s => StudentDto.From(s, includeHealth: true)).ToList();
    }
}
