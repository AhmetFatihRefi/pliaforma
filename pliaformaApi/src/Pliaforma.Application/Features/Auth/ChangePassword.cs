using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;
using ValidationException = Pliaforma.Application.Common.Exceptions.ValidationException;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Authenticated kullanıcı kendi parolasını değiştirir (Sprint 31B-2). Mevcut parola doğrulanır; yeni parola
/// ortak politikadan geçer; başarıda <b>tüm refresh token'lar revoke</b> edilir (yeniden login gerekebilir)
/// ve hesap kilidi sıfırlanır. SecurityAuditLog PasswordChanged yazılır.
/// <para>Reset'ten farkı: bu akış anonim değil; e-posta token yerine MEVCUT parola doğrulanır.</para>
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Unit>;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).Password();   // Sprint 31B-2 ortak politika
    }
}

public sealed class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;
    private readonly TrustedMfaDeviceService _trusted;
    private readonly ISecurityAuditLogger _audit;

    public ChangePasswordHandler(IAppDbContext db, ICurrentUserService current, IPasswordHasher hasher,
        IDateTime clock, AuthSessionService session, TrustedMfaDeviceService trusted, ISecurityAuditLogger audit)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
        _clock = clock;
        _session = session;
        _trusted = trusted;
        _audit = audit;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _current.RequireUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("Kimlik doğrulanmamış.");

        // Mevcut parola yanlış → 400 (alan hatası).
        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Mevcut parola hatalı."],
            });

        user.PasswordHash = _hasher.Hash(request.NewPassword);

        // Sprint 31B-2: parola değişiminde hesap kilidini de sıfırla.
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;

        // Tüm aktif oturumları kapat (yeni parolayla yeniden login gerekebilir).
        await _session.RevokeAllRefreshTokensAsync(user.Id, _clock.UtcNow, cancellationToken);

        // Sprint 50: parola değişince güvenilir cihazlar da iptal (çalıntı cihaz artık MFA atlayamaz).
        await _trusted.RevokeAllAsync(user.Id, _clock.UtcNow, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Denetim: parola değiştirildi. Best-effort (asıl durum zaten kalıcılaştırıldı).
        await _audit.LogAsync(SecurityAuditEventType.PasswordChanged, SecurityAuditOutcome.Success,
            actorUserId: user.Id, actorEmail: user.Email, cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
