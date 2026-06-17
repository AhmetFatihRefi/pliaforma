using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>Üyelik kararı (accept/reject) için yön bazlı yetki kontrolü.</summary>
internal static class MembershipAccess
{
    /// <summary>
    /// InvitedByStudio → yalnızca davet edilen kullanıcı karar verebilir.
    /// RequestedByUser → yalnızca o stüdyonun Owner/StudioAdmin üyesi karar verebilir.
    /// </summary>
    public static async Task EnsureCanDecideAsync(
        IAppDbContext db, ICurrentUserService currentUser, StudioMembership membership, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        if (membership.Direction == MembershipDirection.InvitedByStudio)
        {
            if (membership.UserId != userId)
                throw new ForbiddenException("Bu davet size ait değil.");
            return;
        }

        // RequestedByUser → stüdyo yöneticisi onaylar.
        var isStudioAdmin = await db.StudioMemberships.AnyAsync(
            m => m.StudioId == membership.StudioId
                 && m.UserId == userId
                 && m.Status == MembershipStatus.Active
                 && (m.Role == StudioRole.Owner || m.Role == StudioRole.StudioAdmin),
            ct);

        if (!isStudioAdmin)
            throw new ForbiddenException("Bu başvuruyu onaylama/ret yetkiniz yok.");
    }
}
