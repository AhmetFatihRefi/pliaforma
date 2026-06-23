using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Verilen refresh token'ı revoke eder (best-effort). Token bulunamasa/zaten geçersiz
/// olsa bile sessizce başarılı döner — logout idempotenttir.
/// </summary>
public sealed record LogoutCommand(string? RefreshToken) : ICommand<Unit>;

public sealed class LogoutHandler : ICommandHandler<LogoutCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTime _clock;
    private readonly ISecurityAuditLogger _audit;

    public LogoutHandler(IAppDbContext db, ITokenService tokens, IDateTime clock, ISecurityAuditLogger audit)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Unit.Value;

        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, cancellationToken);

        if (stored is not null)
        {
            stored.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            // Denetim: logout (token revoke edildi). Best-effort.
            await _audit.LogAsync(SecurityAuditEventType.Logout, SecurityAuditOutcome.Success,
                actorUserId: stored.UserId, cancellationToken: cancellationToken);
        }

        return Unit.Value;
    }
}
