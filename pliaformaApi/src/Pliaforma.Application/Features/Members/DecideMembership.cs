using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Bekleyen bir üyeliği kabul eder. Yetki yön bazlıdır (MembershipAccess). Tenant-öncesi olabilir
/// (davet edilen kullanıcının o stüdyoda henüz aktif üyeliği yoktur) → IStudioWriteRequest DEĞİL.
/// </summary>
public sealed record AcceptMembershipCommand(Guid MembershipId) : ICommand<Unit>;

public sealed class AcceptMembershipHandler : ICommandHandler<AcceptMembershipCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public AcceptMembershipHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Unit> Handle(AcceptMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Status != MembershipStatus.Pending)
            throw new BusinessRuleException("Yalnızca bekleyen üyelikler kabul edilebilir.");

        await MembershipAccess.EnsureCanDecideAsync(_db, _currentUser, membership, cancellationToken);

        membership.Status = MembershipStatus.Active;
        membership.AcceptedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Bekleyen bir üyeliği reddeder (yetki yön bazlı).</summary>
public sealed record RejectMembershipCommand(Guid MembershipId) : ICommand<Unit>;

public sealed class RejectMembershipHandler : ICommandHandler<RejectMembershipCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RejectMembershipHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RejectMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Status != MembershipStatus.Pending)
            throw new BusinessRuleException("Yalnızca bekleyen üyelikler reddedilebilir.");

        await MembershipAccess.EnsureCanDecideAsync(_db, _currentUser, membership, cancellationToken);

        membership.Status = MembershipStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
