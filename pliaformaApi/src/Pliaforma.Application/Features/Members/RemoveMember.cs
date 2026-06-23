using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Aktif stüdyodan bir üyeyi çıkarır (Status=Removed). Owner çıkarılamaz. Owner/StudioAdmin yetkisi
/// + aktif stüdyo bağlamı gerektirir (IStudioWriteRequest).
/// </summary>
public sealed record RemoveMemberCommand(Guid MembershipId) : ICommand<Unit>, IStudioWriteRequest;

public sealed class RemoveMemberHandler : ICommandHandler<RemoveMemberCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;

    public RemoveMemberHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Unit> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId && m.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Role == StudioRole.Owner)
            throw new BusinessRuleException("Stüdyo sahibi çıkarılamaz.");

        // Yalnızca Owner, StudioAdmin'i çıkarabilir (StudioAdmin başka StudioAdmin'e dokunamaz).
        if (membership.Role == StudioRole.StudioAdmin && _currentUser.StudioRole != StudioRole.Owner)
            throw new ForbiddenException("Yalnızca stüdyo sahibi bir StudioAdmin'i çıkarabilir.");

        membership.Status = MembershipStatus.Removed;
        await _db.SaveChangesAsync(cancellationToken);

        // Denetim (31C-1): üye çıkarıldı.
        await _audit.LogAsync(SecurityAuditEventType.MemberRemoved, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId,
            targetType: "StudioMembership", targetId: membership.Id,
            detail: $"role: {membership.Role}", cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
