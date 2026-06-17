using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Aktif stüdyonun üyelerini listeler (hoca profili/ücreti dahil). Removed/Rejected hariç tutulabilir.
/// Yetki controller'da Owner/StudioAdmin ile kısıtlanır.
/// </summary>
public sealed record GetMembersQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<MemberDto>>;

public sealed class GetMembersHandler : IQueryHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMembersHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MemberDto>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var query = _db.StudioMemberships.Where(m => m.StudioId == studioId);
        if (!request.IncludeInactive)
            query = query.Where(m => m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Pending);

        // InstructorProfile aynı (StudioId, UserId) ile eşleşir (LEFT JOIN).
        return await query
            .OrderBy(m => m.Role).ThenBy(m => m.User!.FullName)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                m.User!.FullName,
                m.User.Email,
                m.Role,
                m.Status,
                m.Direction,
                Profile = _db.InstructorProfiles
                    .Where(p => p.StudioId == studioId && p.UserId == m.UserId)
                    .Select(p => new { p.Id, p.Rate, p.RateType })
                    .FirstOrDefault(),
            })
            .Select(x => new MemberDto(
                x.Id, x.UserId, x.FullName, x.Email, x.Role, x.Status, x.Direction,
                x.Profile != null ? x.Profile.Id : (Guid?)null,
                x.Profile != null ? x.Profile.Rate : (decimal?)null,
                x.Profile != null ? x.Profile.RateType : (RateType?)null))
            .ToListAsync(cancellationToken);
    }
}
