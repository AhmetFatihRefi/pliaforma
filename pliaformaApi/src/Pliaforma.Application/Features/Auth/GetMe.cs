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

    public GetMeHandler(IAppDbContext db, ICurrentUserService currentUser, AuthSessionService session)
    {
        _db = db;
        _currentUser = currentUser;
        _session = session;
    }

    public async Task<MeResult> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı", userId);

        var memberships = await _session.LoadMembershipsAsync(userId, cancellationToken);
        var active = await _session.BuildActiveStudioAsync(_currentUser.MembershipId, cancellationToken);

        return new MeResult(AuthSessionService.ToUserDto(user), active, memberships);
    }
}
