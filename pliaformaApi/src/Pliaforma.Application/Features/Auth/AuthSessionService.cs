using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Auth akışlarının ortak adımları: token üretimi + refresh token kaydı + üyelik
/// listesi/aktif stüdyo bağlamının oluşturulması. register/login/refresh/switch
/// handler'ları bunu kullanır (mantığı tek yerde tutar).
/// </summary>
public sealed class AuthSessionService
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IMfaChallengeService _challenge;
    private readonly MfaSettings _mfa;
    private readonly IPilotModeInfo _pilotMode;
    private readonly TrustedMfaDeviceService _trustedDevices;

    public AuthSessionService(IAppDbContext db, ITokenService tokens, IMfaChallengeService challenge, IOptions<MfaSettings> mfa, IPilotModeInfo pilotMode, TrustedMfaDeviceService trustedDevices)
    {
        _db = db;
        _tokens = tokens;
        _challenge = challenge;
        _mfa = mfa.Value;
        _pilotMode = pilotMode;
        _trustedDevices = trustedDevices;
    }

    /// <summary>
    /// LOGIN/register-studio için MFA-farkında token üretimi (Sprint 31-MFA):
    /// MFA gerekli + aktif → challenge (tam token YOK); MFA gerekli + kurulmamış → kısıtlı mfa_pending token;
    /// aksi halde normal tam token.
    /// <para>Sprint 50: MFA gerekli + aktif iken geçerli bir "güvenilir cihaz" cookie'si (<paramref name="trustedDeviceToken"/>)
    /// varsa challenge ATLANIR ve tam token verilir (MFA zorunluluğu kalkmaz; yalnız bu cihazda süre boyunca 2. faktör istenmez).</para>
    /// </summary>
    public async Task<AuthOutcome> IssueWithMfaAsync(User user, StudioMembership? activeMembership,
        IReadOnlyList<StudioMembership> activeMemberships, string? ip, string? trustedDeviceToken, CancellationToken ct)
    {
        var required = _mfa.Enabled && MfaPolicy.AccountRequiresMfa(activeMemberships);

        if (required && user.MfaEnabled)
        {
            // Sprint 50: bu cihaz güvenilir mi? (geçerli/expire olmamış/revoke edilmemiş kayıt). Öyleyse challenge atla.
            if (await _trustedDevices.IsTrustedAsync(user.Id, trustedDeviceToken, ct))
            {
                var trustedAuth = await IssueAsync(user, activeMembership, ip, ct, mfaPending: false);
                return new AuthOutcome(trustedAuth, MfaChallengeRequired: false, ChallengeToken: null, MfaSetupRequired: false);
            }

            // MFA aktif → challenge. Bekleyen user state'i (LastLoginAt, sayaç sıfırlama) kalıcılaştır; tam token verme.
            await _db.SaveChangesAsync(ct);
            return new AuthOutcome(null, MfaChallengeRequired: true, _challenge.CreateChallengeToken(user.Id), MfaSetupRequired: false);
        }

        var auth = await IssueAsync(user, activeMembership, ip, ct, mfaPending: required);
        return new AuthOutcome(auth, MfaChallengeRequired: false, ChallengeToken: null, MfaSetupRequired: required);
    }

    /// <summary>
    /// REFRESH için: kullanıcı zaten MFA geçmiş (geçerli refresh token) → challenge YOK. Yalnız setup-pending
    /// (MFA gerekli ama kurulmamış) durumunda kısıtlı token üretilir; full'a dönülmez.
    /// </summary>
    public async Task<AuthOutcome> IssuePostAuthAsync(User user, StudioMembership? activeMembership,
        IReadOnlyList<StudioMembership> activeMemberships, string? ip, CancellationToken ct)
    {
        var setupPending = _mfa.Enabled && !user.MfaEnabled && MfaPolicy.AccountRequiresMfa(activeMemberships);
        var auth = await IssueAsync(user, activeMembership, ip, ct, mfaPending: setupPending);
        return new AuthOutcome(auth, MfaChallengeRequired: false, ChallengeToken: null, MfaSetupRequired: setupPending);
    }

    /// <summary>
    /// Kullanıcı için access + refresh token üretir, refresh'i hash'leyip kaydeder ve
    /// üyelik listesi + (varsa) aktif stüdyo bağlamı ile AuthResult döner.
    /// </summary>
    public async Task<AuthResult> IssueAsync(User user, StudioMembership? activeMembership, string? ip, CancellationToken ct, bool mfaPending = false)
    {
        var refresh = _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ip,
        });
        await _db.SaveChangesAsync(ct);

        return await BuildResultAsync(user, activeMembership, refresh.RawToken, refresh.ExpiresAt, ct, mfaPending);
    }

    /// <summary>
    /// Yeni refresh token ÜRETMEDEN yalnızca access token üretir (switch-studio için).
    /// Sonuçtaki RefreshToken boştur; controller bu durumda cookie'ye dokunmaz.
    /// </summary>
    public async Task<AuthResult> IssueAccessOnlyAsync(User user, StudioMembership? activeMembership, CancellationToken ct) =>
        await BuildResultAsync(user, activeMembership, refreshRawToken: string.Empty, refreshExpiresAt: default, ct);

    private async Task<AuthResult> BuildResultAsync(User user, StudioMembership? activeMembership, string refreshRawToken, DateTime refreshExpiresAt, CancellationToken ct, bool mfaPending = false)
    {
        var access = _tokens.GenerateAccessToken(user, activeMembership, mfaPending);
        var memberships = await LoadMembershipsAsync(user.Id, ct);
        var active = await BuildActiveStudioAsync(activeMembership?.Id, ct);

        return new AuthResult(
            access.Token,
            access.ExpiresAt,
            refreshRawToken,
            refreshExpiresAt,
            ToUserDto(user),
            active,
            memberships,
            _pilotMode.SimplePeopleModeEnabled);
    }

    public async Task<IReadOnlyList<MembershipDto>> LoadMembershipsAsync(Guid userId, CancellationToken ct) =>
        await _db.StudioMemberships
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.Studio!.Name)
            .Select(m => new MembershipDto(
                m.Id, m.StudioId, m.Studio!.Name, m.Studio.Slug, m.Role, m.Status))
            .ToListAsync(ct);

    public async Task<ActiveStudioDto?> BuildActiveStudioAsync(Guid? membershipId, CancellationToken ct)
    {
        if (membershipId is null)
            return null;

        return await _db.StudioMemberships
            .Where(m => m.Id == membershipId && m.Status == MembershipStatus.Active)
            .Select(m => new ActiveStudioDto(
                m.Id, m.StudioId, m.Studio!.Name, m.Studio.Slug, m.Role))
            .FirstOrDefaultAsync(ct);
    }

    public static UserDto ToUserDto(User user) =>
        new(user.Id, user.Email, user.FullName, user.Phone, user.GlobalRole, user.EmailVerifiedAt is not null,
            user.RegistrationIntent, user.MfaEnabled);

    /// <summary>Kullanıcının tüm aktif refresh tokenlarını iptal eder (şifre sıfırlama sonrası — Sprint 16).</summary>
    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active)
            t.RevokedAt = nowUtc;
    }
}
