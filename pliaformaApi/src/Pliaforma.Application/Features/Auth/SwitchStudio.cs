using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Mevcut (kimliği doğrulanmış) kullanıcının access token'ını seçilen stüdyo bağlamıyla
/// yeniden üretir. Üyeliğin hâlâ Active olduğu DB'den doğrulanır; değilse reddedilir.
/// Refresh token'a dokunulmaz.
/// </summary>
public sealed record SwitchStudioCommand(Guid StudioId) : ICommand<AuthResult>;

public sealed class SwitchStudioHandler : ICommandHandler<SwitchStudioCommand, AuthResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AuthSessionService _session;

    public SwitchStudioHandler(IAppDbContext db, ICurrentUserService currentUser, AuthSessionService session)
    {
        _db = db;
        _currentUser = currentUser;
        _session = session;
    }

    public async Task<AuthResult> Handle(SwitchStudioCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("Oturum geçersiz.");

        if (!user.IsActive)
            throw new ForbiddenException("Hesabınız devre dışı bırakılmış.");

        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.StudioId == request.StudioId && m.Status == MembershipStatus.Active,
                cancellationToken)
            ?? throw new ForbiddenException("Bu stüdyoda aktif bir üyeliğiniz yok.");

        return await _session.IssueAccessOnlyAsync(user, membership, cancellationToken);
    }
}
