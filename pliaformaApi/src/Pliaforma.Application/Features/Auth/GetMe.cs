using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>Mevcut kullanıcı + aktif stüdyo bağlamı + üyelik listesini döner.</summary>
public sealed record GetMeQuery : IQuery<MeResult>;

public sealed class GetMeHandler : IQueryHandler<GetMeQuery, MeResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AuthSessionService _session;
    private readonly IPilotModeInfo _pilotMode;

    public GetMeHandler(IAppDbContext db, ICurrentUserService currentUser, AuthSessionService session, IPilotModeInfo pilotMode)
    {
        _db = db;
        _currentUser = currentUser;
        _session = session;
        _pilotMode = pilotMode;
    }

    public async Task<MeResult> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı", userId);

        var memberships = await _session.LoadMembershipsAsync(userId, cancellationToken);
        var active = await _session.BuildActiveStudioAsync(_currentUser.MembershipId, cancellationToken);

        // Sprint 31-MFA: kısıtlı (mfa_pending) token ise setup gerekli; aktifse kalan recovery code sayısı.
        var recoveryRemaining = user.MfaEnabled
            ? await _db.MfaRecoveryCodes.CountAsync(c => c.UserId == userId && c.ConsumedAt == null, cancellationToken)
            : 0;

        return new MeResult(AuthSessionService.ToUserDto(user), active, memberships,
            _currentUser.MfaPending, recoveryRemaining, _pilotMode.SimplePeopleModeEnabled);
    }
}
