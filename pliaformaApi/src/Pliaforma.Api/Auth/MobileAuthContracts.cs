using Pliaforma.Application.Features.Auth;

namespace Pliaforma.Api.Auth;

// Sprint 53 — mobil (native) istemci istek gövdeleri. Web'den tek fark taşımadır:
// refresh + trusted-device token'ları cookie yerine BODY ile taşınır (mobilde cookie/origin yok).
public sealed record MobileLoginRequest(string Email, string Password, string? TrustedDeviceToken = null);
public sealed record MobileRefreshRequest(string RefreshToken, Guid? StudioId = null);
public sealed record MobileLogoutRequest(string? RefreshToken = null);
public sealed record MobileMfaChallengeVerifyRequest(string ChallengeToken, string Code, bool? RememberDevice = null);
public sealed record MobileMfaRecoveryCodeVerifyRequest(string ChallengeToken, string RecoveryCode);
public sealed record MobileMfaSetupVerifyRequest(string Code);

/// <summary>
/// Mobil auth yanıtı. Web <see cref="AuthResponse"/>'tan farkı: <c>RefreshToken</c> (+ rememberDevice
/// istendiyse <c>TrustedDeviceToken</c>) BODY'dedir — mobil istemci bunları OS güvenli deposunda
/// (iOS Keychain / Android Keystore) saklamak ZORUNDADIR. Bu yanıtı dönen uçlar cache'i kapatır
/// (Cache-Control: no-store). Web yanıtına refresh token GİRMEZ; web modeli değişmemiştir.
/// </summary>
public sealed record MobileAuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User,
    ActiveStudioDto? ActiveStudio,
    IReadOnlyList<MembershipDto> Memberships,
    bool MfaSetupRequired = false,
    string? TrustedDeviceToken = null,
    DateTime? TrustedDeviceExpiresAt = null)
{
    public static MobileAuthResponse From(AuthResult r, bool mfaSetupRequired = false,
        string? trustedDeviceToken = null, DateTime? trustedDeviceExpiresAt = null) =>
        new(r.AccessToken, r.AccessTokenExpiresAt, r.RefreshToken, r.RefreshTokenExpiresAt,
            r.User, r.ActiveStudio, r.Memberships, mfaSetupRequired, trustedDeviceToken, trustedDeviceExpiresAt);
}

/// <summary>Mobil mfa/setup/verify yanıtı: tam auth + bir kez gösterilecek recovery code'lar.</summary>
public sealed record MobileMfaSetupCompleteResponse(MobileAuthResponse Auth, IReadOnlyList<string> RecoveryCodes);
