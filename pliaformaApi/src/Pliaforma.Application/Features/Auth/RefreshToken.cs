using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Geçerli bir refresh token ile yeni access token üretir ve refresh token'ı rotasyonlar
/// (eskisini revoke eder, yenisini verir). Üretim sırasında kullanıcı + (varsa) seçilen
/// stüdyodaki üyelik HÂLÂ Active mi DB'den doğrulanır.
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    Guid? StudioId = null,
    string? Ip = null) : ICommand<AuthResult>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenHandler : ICommandHandler<RefreshTokenCommand, AuthResult>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;

    public RefreshTokenHandler(IAppDbContext db, ITokenService tokens, IDateTime clock, AuthSessionService session)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _session = session;
    }

    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null || _clock.UtcNow >= stored.ExpiresAt)
            throw new UnauthorizedException("Oturum geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapın.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken)
            ?? throw new UnauthorizedException("Oturum geçersiz.");

        if (!user.IsActive)
            throw new ForbiddenException("Hesabınız devre dışı bırakılmış.");

        // İstenen stüdyo bağlamı varsa üyeliğin hâlâ Active olduğunu doğrula.
        StudioMembership? membership = null;
        if (request.StudioId is { } studioId)
        {
            membership = await _db.StudioMemberships
                .FirstOrDefaultAsync(
                    m => m.UserId == user.Id && m.StudioId == studioId && m.Status == MembershipStatus.Active,
                    cancellationToken)
                ?? throw new ForbiddenException("Stüdyo üyeliğiniz artık aktif değil.");
        }

        // Rotasyon: eski token'ı revoke et, yenisini IssueAsync üretsin.
        stored.RevokedAt = _clock.UtcNow;

        return await _session.IssueAsync(user, membership, request.Ip, cancellationToken);
    }
}
