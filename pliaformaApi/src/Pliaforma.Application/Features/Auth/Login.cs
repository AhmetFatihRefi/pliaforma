using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Kimlik doğrular ve token üretir. Kullanıcının tam olarak bir aktif üyeliği varsa o
/// stüdyo otomatik seçilir; birden fazla varsa token stüdyosuz döner ve istemci
/// switch-studio ile seçer.
/// <para>Sprint 31B-2: hesap kilitleme (N başarısız → geçici kilit) + güvenlik denetimi (audit).</para>
/// </summary>
// Sprint 50: TrustedDeviceToken — istemcinin "bu cihazı hatırla" cookie'sinin ham değeri (controller okur).
public sealed record LoginCommand(string Email, string Password, string? Ip = null, string? TrustedDeviceToken = null) : ICommand<AuthOutcome>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthOutcome>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;
    private readonly ISecurityAuditLogger _audit;
    private readonly ILoginAttemptStore _loginAttempts;
    private readonly AuthSettings _auth;

    public LoginHandler(IAppDbContext db, IPasswordHasher hasher, IDateTime clock, AuthSessionService session,
        ISecurityAuditLogger audit, ILoginAttemptStore loginAttempts, IOptions<AuthSettings> auth)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _session = session;
        _audit = audit;
        _loginAttempts = loginAttempts;
        _auth = auth.Value;
    }

    public async Task<AuthOutcome> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var now = _clock.UtcNow;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Kullanıcı yok → generic 401 (sayaç/satır yok, enumeration minimal). Deneme yine denetlenir.
        if (user is null)
        {
            await _audit.LogAsync(SecurityAuditEventType.LoginFailed, SecurityAuditOutcome.Failure,
                actorEmail: request.Email, detail: "unknown_email", cancellationToken: cancellationToken);
            throw new UnauthorizedException("E-posta veya parola hatalı.");
        }

        // Hesap geçici kilitli → 423 (generic mesaj; hesabın var olduğunu çok açık etmez).
        // Kilitleme kapalıysa (DEV) mevcut kilit de yok sayılır.
        if (_auth.EnableLockout && user.LockoutEnd is { } until && until > now)
        {
            await _audit.LogAsync(SecurityAuditEventType.LoginFailed, SecurityAuditOutcome.Failure,
                actorUserId: user.Id, actorEmail: user.Email, detail: "locked", cancellationToken: cancellationToken);
            throw new LockedException("Giriş geçici olarak kilitlendi. Lütfen bir süre sonra tekrar deneyin.");
        }

        // Parola yanlış → sayaç artır; eşikte geçici kilit. Bu yazımlar BAĞIMSIZ bağlamla yapılır (login komutu
        // 401 fırlatıp TransactionBehavior rollback yapsa bile sayaç/kilit/audit KALICI olur).
        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            var lockedNow = await _loginAttempts.RecordFailedAttemptAsync(user.Id, cancellationToken);

            await _audit.LogAsync(SecurityAuditEventType.LoginFailed, SecurityAuditOutcome.Failure,
                actorUserId: user.Id, actorEmail: user.Email, detail: "invalid_password", cancellationToken: cancellationToken);
            if (lockedNow)
                await _audit.LogAsync(SecurityAuditEventType.AccountLockedOut, SecurityAuditOutcome.Failure,
                    actorUserId: user.Id, actorEmail: user.Email, cancellationToken: cancellationToken);

            throw new UnauthorizedException("E-posta veya parola hatalı.");
        }

        if (!user.IsActive)
            throw new ForbiddenException("Hesabınız devre dışı bırakılmış.");

        // Başarılı: kilit/sayaç sıfırla, son giriş zamanı.
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now;

        var activeMemberships = await _db.StudioMemberships
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .ToListAsync(cancellationToken);

        // Tek aktif üyelik varsa otomatik seç; aksi halde stüdyosuz token.
        var activeMembership = activeMemberships.Count == 1 ? activeMemberships[0] : null;

        // Sprint 31-MFA: MFA gerekiyorsa challenge / setup-pending dallanması.
        // Sprint 50: geçerli güvenilir-cihaz cookie'si varsa challenge atlanır (trustedDeviceToken).
        var outcome = await _session.IssueWithMfaAsync(user, activeMembership, activeMemberships, request.Ip, request.TrustedDeviceToken, cancellationToken);

        if (outcome.MfaChallengeRequired)
            await _audit.LogAsync(SecurityAuditEventType.MFAChallengeCreated, SecurityAuditOutcome.Success,
                actorUserId: user.Id, actorEmail: user.Email, cancellationToken: cancellationToken);
        else
            await _audit.LogAsync(SecurityAuditEventType.LoginSucceeded, SecurityAuditOutcome.Success,
                actorUserId: user.Id, actorEmail: user.Email, studioId: activeMembership?.StudioId,
                cancellationToken: cancellationToken);

        return outcome;
    }
}
