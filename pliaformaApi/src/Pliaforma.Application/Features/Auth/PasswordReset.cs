using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
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
    private readonly ISecurityAuditLogger _audit;

    public ForgotPasswordHandler(IAppDbContext db, EmailTokenIssuer tokens, IEmailSender email,
        ISecurityAuditLogger audit)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
        _audit = audit;
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

            // Denetim: parola sıfırlama TALEBİ (token üretildi). Best-effort (akışı kırmaz).
            await _audit.LogAsync(SecurityAuditEventType.PasswordResetRequested, SecurityAuditOutcome.Success,
                actorUserId: user.Id, actorEmail: user.Email, cancellationToken: cancellationToken);
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
        RuleFor(x => x.NewPassword).Password();   // Sprint 31B-2 ortak politika
    }
}

public sealed class ResetPasswordHandler : ICommandHandler<ResetPasswordCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly EmailTokenIssuer _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;
    private readonly TrustedMfaDeviceService _trusted;
    private readonly ISecurityAuditLogger _audit;

    public ResetPasswordHandler(IAppDbContext db, EmailTokenIssuer tokens, IPasswordHasher hasher,
        IDateTime clock, AuthSessionService session, TrustedMfaDeviceService trusted, ISecurityAuditLogger audit)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _clock = clock;
        _session = session;
        _trusted = trusted;
        _audit = audit;
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

        // Sprint 31B-2: parola yenilendiğine göre hesap kilidini de aç (kullanıcı sahipliğini kanıtladı).
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;

        // Çalınmış oturumları kapat: tüm aktif refresh tokenları revoke et.
        await _session.RevokeAllRefreshTokensAsync(user.Id, _clock.UtcNow, cancellationToken);

        // Sprint 50: parola sıfırlanınca güvenilir cihazları da iptal et.
        await _trusted.RevokeAllAsync(user.Id, _clock.UtcNow, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Denetim: parola sıfırlama TAMAMLANDI. Best-effort (asıl durum zaten kalıcılaştırıldı).
        await _audit.LogAsync(SecurityAuditEventType.PasswordResetCompleted, SecurityAuditOutcome.Success,
            actorUserId: user.Id, actorEmail: user.Email, cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
