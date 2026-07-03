using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;
using ValidationException = Pliaforma.Application.Common.Exceptions.ValidationException;

namespace Pliaforma.Application.Features.Auth;

// ============================ Result DTO'ları ============================
public sealed record MfaSetupStartResult(string OtpauthUri, string ManualKey);
public sealed record MfaSetupVerifyResult(AuthResult Auth, IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// Sprint 50 — MFA challenge doğrulama sonucu. <see cref="TrustedDeviceToken"/> yalnız "bu cihazı hatırla"
/// işaretliyse doludur; controller onu httpOnly güvenilir-cihaz cookie'sine yazar (body'ye KOYMAZ).
/// </summary>
public sealed record MfaVerifyOutcome(AuthResult Auth, string? TrustedDeviceToken, DateTime? TrustedDeviceExpiresAt);

internal static class MfaSupport
{
    public static StudioMembership? PickActive(List<StudioMembership> active) => active.Count == 1 ? active[0] : null;

    public static Task<List<StudioMembership>> ActiveMembershipsAsync(IAppDbContext db, Guid userId, CancellationToken ct) =>
        db.StudioMemberships.Where(m => m.UserId == userId && m.Status == MembershipStatus.Active).ToListAsync(ct);
}

// ============================ setup/start ============================
public sealed record MfaSetupStartCommand : ICommand<MfaSetupStartResult>;

public sealed class MfaSetupStartHandler : ICommandHandler<MfaSetupStartCommand, MfaSetupStartResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ITotpService _totp;
    private readonly IMfaSecretProtector _protector;
    private readonly ISecurityAuditLogger _audit;
    private readonly IDateTime _clock;
    private readonly MfaSettings _mfa;

    public MfaSetupStartHandler(IAppDbContext db, ICurrentUserService current, ITotpService totp,
        IMfaSecretProtector protector, ISecurityAuditLogger audit, IDateTime clock, IOptions<MfaSettings> mfa)
    {
        _db = db; _current = current; _totp = totp; _protector = protector; _audit = audit; _clock = clock; _mfa = mfa.Value;
    }

    public async Task<MfaSetupStartResult> Handle(MfaSetupStartCommand request, CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("Kimlik doğrulanmamış.");

        var secret = _totp.GenerateSecret();
        var settings = await _db.UserMfaSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (settings is null)
        {
            settings = new UserMfaSettings { UserId = userId };
            _db.UserMfaSettings.Add(settings);
        }
        settings.PendingSecretEncrypted = _protector.Protect(secret);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(SecurityAuditEventType.MFASetupStarted, SecurityAuditOutcome.Success,
            actorUserId: userId, actorEmail: user.Email, cancellationToken: ct);

        return new MfaSetupStartResult(_totp.BuildOtpAuthUri(secret, user.Email, _mfa.Issuer), secret);
    }
}

// ============================ setup/verify ============================
// Sprint 53: Client — üretilecek refresh token'ın kanalı (web cookie / mobil body).
public sealed record MfaSetupVerifyCommand(string Code, string? Ip = null, AuthClient Client = AuthClient.Web) : ICommand<MfaSetupVerifyResult>;

public sealed class MfaSetupVerifyHandler : ICommandHandler<MfaSetupVerifyCommand, MfaSetupVerifyResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ITotpService _totp;
    private readonly IMfaSecretProtector _protector;
    private readonly ISecurityAuditLogger _audit;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;
    private readonly TrustedMfaDeviceService _trusted;
    private readonly MfaSettings _mfa;

    public MfaSetupVerifyHandler(IAppDbContext db, ICurrentUserService current, ITotpService totp,
        IMfaSecretProtector protector, ISecurityAuditLogger audit, IDateTime clock, AuthSessionService session,
        TrustedMfaDeviceService trusted, IOptions<MfaSettings> mfa)
    {
        _db = db; _current = current; _totp = totp; _protector = protector; _audit = audit; _clock = clock;
        _session = session; _trusted = trusted; _mfa = mfa.Value;
    }

    public async Task<MfaSetupVerifyResult> Handle(MfaSetupVerifyCommand request, CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("Kimlik doğrulanmamış.");
        var settings = await _db.UserMfaSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (settings?.PendingSecretEncrypted is null)
            throw new BusinessRuleException("Önce MFA kurulumunu başlatın (setup/start).");

        var secret = _protector.Unprotect(settings.PendingSecretEncrypted);
        if (!_totp.VerifyCode(secret, request.Code, lastUsedTimestep: 0, out var matchedTimestep))
            throw new ValidationException(new Dictionary<string, string[]> { ["code"] = ["Kod hatalı. Authenticator uygulamasındaki güncel kodu girin."] });

        // Aktive et: pending → aktif secret.
        settings.SecretEncrypted = settings.PendingSecretEncrypted;
        settings.PendingSecretEncrypted = null;
        settings.EnrolledAt = _clock.UtcNow;
        settings.LastUsedTimestep = matchedTimestep;
        user.MfaEnabled = true;

        // Sprint 50: (yeniden) kurulumda eski güvenilir cihazları iptal et (ilk kurulumda no-op).
        await _trusted.RevokeAllAsync(userId, _clock.UtcNow, ct);

        // Recovery code'lar: plain bir kez döner, hash'li saklanır.
        var plainCodes = new List<string>();
        for (int i = 0; i < _mfa.RecoveryCodeCount; i++)
        {
            var code = Common.Security.RecoveryCodes.Generate();
            plainCodes.Add(code);
            _db.MfaRecoveryCodes.Add(new MfaRecoveryCode { UserId = userId, CodeHash = Common.Security.RecoveryCodes.Hash(code) });
        }

        var active = await MfaSupport.ActiveMembershipsAsync(_db, userId, ct);
        var auth = await _session.IssueAsync(user, MfaSupport.PickActive(active), request.Ip, ct, client: request.Client); // artık tam token

        await _audit.LogAsync(SecurityAuditEventType.MFASetupCompleted, SecurityAuditOutcome.Success,
            actorUserId: userId, actorEmail: user.Email, cancellationToken: ct);

        return new MfaSetupVerifyResult(auth, plainCodes);
    }
}

// ============================ challenge/verify ============================
// Sprint 50: RememberDevice — "bu cihazda 30 gün tekrar sorma" işaretiyse güvenilir cihaz kaydı + cookie üretilir.
// Sprint 53: Client=Mobile iken ham trusted token'ı controller cookie yerine BODY'ye koyar (mobil güvenli depo).
public sealed record MfaChallengeVerifyCommand(string ChallengeToken, string Code, bool RememberDevice = false, string? Ip = null,
    AuthClient Client = AuthClient.Web) : ICommand<MfaVerifyOutcome>;

public sealed class MfaChallengeVerifyHandler : ICommandHandler<MfaChallengeVerifyCommand, MfaVerifyOutcome>
{
    private readonly IAppDbContext _db;
    private readonly IMfaChallengeService _challenge;
    private readonly ITotpService _totp;
    private readonly IMfaSecretProtector _protector;
    private readonly ISecurityAuditLogger _audit;
    private readonly AuthSessionService _session;
    private readonly TrustedMfaDeviceService _trusted;

    public MfaChallengeVerifyHandler(IAppDbContext db, IMfaChallengeService challenge, ITotpService totp,
        IMfaSecretProtector protector, ISecurityAuditLogger audit, AuthSessionService session, TrustedMfaDeviceService trusted)
    {
        _db = db; _challenge = challenge; _totp = totp; _protector = protector; _audit = audit; _session = session; _trusted = trusted;
    }

    public async Task<MfaVerifyOutcome> Handle(MfaChallengeVerifyCommand request, CancellationToken ct)
    {
        var userId = _challenge.ValidateChallengeToken(request.ChallengeToken)
            ?? throw new UnauthorizedException("MFA oturumu geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapın.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        var settings = await _db.UserMfaSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (user is null || !user.MfaEnabled || settings?.SecretEncrypted is null)
            throw new UnauthorizedException("MFA doğrulanamadı.");

        var secret = _protector.Unprotect(settings.SecretEncrypted);
        if (!_totp.VerifyCode(secret, request.Code, settings.LastUsedTimestep ?? 0, out var matchedTimestep))
        {
            await _audit.LogAsync(SecurityAuditEventType.MFAFailed, SecurityAuditOutcome.Failure,
                actorUserId: userId, actorEmail: user.Email, detail: "invalid_totp", cancellationToken: ct);
            throw new UnauthorizedException("Kod hatalı.");
        }

        settings.LastUsedTimestep = matchedTimestep; // replay koruması

        // Sprint 50: "bu cihazı hatırla" işaretliyse güvenilir cihaz kaydı EKLE (IssueAsync SaveChanges'ıyla kalıcılaşır).
        TrustedDeviceIssue? trustedIssue = request.RememberDevice ? _trusted.Remember(userId, request.Ip) : null;

        var active = await MfaSupport.ActiveMembershipsAsync(_db, userId, ct);
        var auth = await _session.IssueAsync(user, MfaSupport.PickActive(active), request.Ip, ct, client: request.Client);

        await _audit.LogAsync(SecurityAuditEventType.MFASucceeded, SecurityAuditOutcome.Success,
            actorUserId: userId, actorEmail: user.Email, cancellationToken: ct);

        return new MfaVerifyOutcome(auth, trustedIssue?.RawToken, trustedIssue?.ExpiresAt);
    }
}

// ============================ recovery-code/verify ============================
// Sprint 53: Client — refresh token kanalı. Recovery-code yolu trusted device ÜRETMEZ (kanaldan bağımsız kural).
public sealed record MfaRecoveryCodeVerifyCommand(string ChallengeToken, string RecoveryCode, string? Ip = null,
    AuthClient Client = AuthClient.Web) : ICommand<AuthResult>;

public sealed class MfaRecoveryCodeVerifyHandler : ICommandHandler<MfaRecoveryCodeVerifyCommand, AuthResult>
{
    private readonly IAppDbContext _db;
    private readonly IMfaChallengeService _challenge;
    private readonly ISecurityAuditLogger _audit;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;

    public MfaRecoveryCodeVerifyHandler(IAppDbContext db, IMfaChallengeService challenge, ISecurityAuditLogger audit,
        IDateTime clock, AuthSessionService session)
    {
        _db = db; _challenge = challenge; _audit = audit; _clock = clock; _session = session;
    }

    public async Task<AuthResult> Handle(MfaRecoveryCodeVerifyCommand request, CancellationToken ct)
    {
        var userId = _challenge.ValidateChallengeToken(request.ChallengeToken)
            ?? throw new UnauthorizedException("MFA oturumu geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapın.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("MFA doğrulanamadı.");

        var hash = Common.Security.RecoveryCodes.Hash(request.RecoveryCode);
        var match = await _db.MfaRecoveryCodes
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == hash && c.ConsumedAt == null, ct);

        if (match is null)
        {
            await _audit.LogAsync(SecurityAuditEventType.MFAFailed, SecurityAuditOutcome.Failure,
                actorUserId: userId, actorEmail: user.Email, detail: "invalid_recovery_code", cancellationToken: ct);
            throw new UnauthorizedException("Recovery code hatalı veya kullanılmış.");
        }

        match.ConsumedAt = _clock.UtcNow;
        var active = await MfaSupport.ActiveMembershipsAsync(_db, userId, ct);
        var auth = await _session.IssueAsync(user, MfaSupport.PickActive(active), request.Ip, ct, client: request.Client);

        await _audit.LogAsync(SecurityAuditEventType.MFARecoveryCodeUsed, SecurityAuditOutcome.Success,
            actorUserId: userId, actorEmail: user.Email, cancellationToken: ct);

        return auth;
    }
}

// ============================ reset (PlatformAdmin) ============================
public sealed record MfaResetCommand(Guid TargetUserId) : ICommand<Unit>;

public sealed class MfaResetHandler : ICommandHandler<MfaResetCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISecurityAuditLogger _audit;
    private readonly TrustedMfaDeviceService _trusted;
    private readonly IDateTime _clock;

    public MfaResetHandler(IAppDbContext db, ICurrentUserService current, ISecurityAuditLogger audit,
        TrustedMfaDeviceService trusted, IDateTime clock)
    {
        _db = db; _current = current; _audit = audit; _trusted = trusted; _clock = clock;
    }

    public async Task<Unit> Handle(MfaResetCommand request, CancellationToken ct)
    {
        // Yetki: yalnız PlatformAdmin (controller [Authorize(Policy=PlatformAdmin)] + burada ikinci katman).
        if (_current.GlobalRole != GlobalRole.PlatformAdmin)
            throw new ForbiddenException("Bu işlem yalnız PlatformAdmin tarafından yapılabilir.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId, ct)
            ?? throw new NotFoundException("Kullanıcı", request.TargetUserId);

        user.MfaEnabled = false;

        var settings = await _db.UserMfaSettings.FirstOrDefaultAsync(x => x.UserId == request.TargetUserId, ct);
        if (settings is not null)
        {
            settings.SecretEncrypted = null;
            settings.PendingSecretEncrypted = null;
            settings.EnrolledAt = null;
            settings.LastUsedTimestep = null;
        }

        var codes = await _db.MfaRecoveryCodes.Where(c => c.UserId == request.TargetUserId).ToListAsync(ct);
        _db.MfaRecoveryCodes.RemoveRange(codes); // soft-delete (AuditInterceptor)

        // Sprint 50: MFA reset → hedef kullanıcının tüm güvenilir cihazlarını iptal et.
        await _trusted.RevokeAllAsync(request.TargetUserId, _clock.UtcNow, ct);

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(SecurityAuditEventType.MFAResetCompleted, SecurityAuditOutcome.Success,
            actorUserId: _current.UserId, targetType: "User", targetId: request.TargetUserId, cancellationToken: ct);

        return Unit.Value;
    }
}
