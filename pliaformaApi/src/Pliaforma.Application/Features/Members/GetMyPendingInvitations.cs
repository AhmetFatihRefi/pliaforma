using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Mevcut kullanıcıya gelen, bekleyen stüdyo davetlerini listeler (stüdyodan davet edilenler).
/// Tenant-öncesidir (herhangi bir stüdyo bağlamı gerekmez) — yalnızca UserId ile filtreler.
/// </summary>
public sealed record GetMyPendingInvitationsQuery : IQuery<IReadOnlyList<PendingInvitationDto>>;

public sealed class GetMyPendingInvitationsHandler
    : IQueryHandler<GetMyPendingInvitationsQuery, IReadOnlyList<PendingInvitationDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyPendingInvitationsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PendingInvitationDto>> Handle(GetMyPendingInvitationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        return await _db.StudioMemberships
            .Where(m => m.UserId == userId
                        && m.Status == MembershipStatus.Pending
                        && m.Direction == MembershipDirection.InvitedByStudio)
            .OrderBy(m => m.Studio!.Name)
            .Select(m => new PendingInvitationDto(m.Id, m.StudioId, m.Studio!.Name, m.Studio.Slug, m.Role))
            .ToListAsync(cancellationToken);
    }
}
