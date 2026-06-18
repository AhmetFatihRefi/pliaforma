using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

// ---- Şifremi unuttum (anon; her durumda generic 200; enumeration yok) ----
public sealed record ForgotPasswordCommand(string Email, string? Ip) : ICommand<Unit>;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ForgotPasswordHandler : ICommandHandler<ForgotPasswordCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly EmailTokenIssuer _tokens;
    private readonly IEmailSender _email;

    public ForgotPasswordHandler(IAppDbContext db, EmailTokenIssuer tokens, IEmailSender email)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalized = EmailNormalizer.Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        // Kullanıcı varsa + cooldown dışındaysa token + mail. Var/yok her durumda generic 200 (sızdırmaz).
        if (user is not null
            && !await _tokens.InCooldownAsync(user.Id, AuthTokenPurpose.PasswordReset, AuthTokenRules.ResendCooldown, cancellationToken))
        {
            var rawToken = await _tokens.IssueAsync(
                user.Id, AuthTokenPurpose.PasswordReset, AuthTokenRules.PasswordResetTtl, request.Ip, null, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _email.SendPasswordResetAsync(user.Email, user.FullName, rawToken, cancellationToken);
        }

        return Unit.Value;
    }
}

// ---- Şifre sıfırla (anon; token + yeni şifre) ----
public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Unit>;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class ResetPasswordHandler : ICommandHandler<ResetPasswordCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly EmailTokenIssuer _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;

    public ResetPasswordHandler(IAppDbContext db, EmailTokenIssuer tokens, IPasswordHasher hasher,
        IDateTime clock, AuthSessionService session)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _clock = clock;
        _session = session;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var token = await _tokens.ConsumeAsync(request.Token, AuthTokenPurpose.PasswordReset, cancellationToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            ?? throw new BusinessRuleException("Bağlantı geçersiz veya süresi dolmuş.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);

        // Karar #6: reset linkine erişen kullanıcı e-postasını kontrol ettiğini kanıtlamıştır → doğrula.
        if (user.EmailVerifiedAt is null)
            user.EmailVerifiedAt = _clock.UtcNow;

        // Çalınmış oturumları kapat: tüm aktif refresh tokenları revoke et.
        await _session.RevokeAllRefreshTokensAsync(user.Id, _clock.UtcNow, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
