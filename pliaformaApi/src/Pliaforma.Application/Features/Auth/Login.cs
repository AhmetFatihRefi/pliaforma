using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Kimlik doğrular ve token üretir. Kullanıcının tam olarak bir aktif üyeliği varsa o
/// stüdyo otomatik seçilir; birden fazla varsa token stüdyosuz döner ve istemci
/// switch-studio ile seçer.
/// </summary>
public sealed record LoginCommand(string Email, string Password, string? Ip = null) : ICommand<AuthResult>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthResult>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;

    public LoginHandler(IAppDbContext db, IPasswordHasher hasher, IDateTime clock, AuthSessionService session)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _session = session;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Aynı hata mesajı: e-posta var mı yok mu sızdırmamak için.
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("E-posta veya parola hatalı.");

        if (!user.IsActive)
            throw new ForbiddenException("Hesabınız devre dışı bırakılmış.");

        user.LastLoginAt = _clock.UtcNow;

        var activeMemberships = await _db.StudioMemberships
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .ToListAsync(cancellationToken);

        // Tek aktif üyelik varsa otomatik seç; aksi halde stüdyosuz token.
        var activeMembership = activeMemberships.Count == 1 ? activeMemberships[0] : null;

        return await _session.IssueAsync(user, activeMembership, request.Ip, cancellationToken);
    }
}
