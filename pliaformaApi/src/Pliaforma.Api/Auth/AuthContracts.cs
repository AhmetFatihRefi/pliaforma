using Pliaforma.Application.Features.Auth;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Auth;

// İstek gövdeleri (Ip gibi sunucu-tarafı alanları client'tan AYRI tutulur).
public sealed record RegisterStudioRequest(string StudioName, string OwnerFullName, string Email, string Password, string? Phone);
// Sprint 17 Rev2: Intent (UX niyeti) opsiyonel — null → Unknown.
public sealed record RegisterRequest(string FullName, string Email, string Password, string? Phone, RegistrationIntent? Intent);
public sealed record LoginRequest(string Email, string Password);

// Sprint 49: anonim; frontend'in "Stüdyo oluştur" yüzeylerini gösterip göstermeyeceğine karar vermesi için.
public sealed record RegistrationStatusResponse(bool StudioRegistrationEnabled);
public sealed record SwitchStudioRequest(Guid StudioId);
public sealed record RefreshRequest(Guid? StudioId);

// Sprint 16: e-posta doğrulama + şifre sıfırlama
public sealed record VerifyEmailRequest(string Token);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);

// Sprint 31B-2: authenticated parola değiştirme
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// Sprint 31-MFA: MFA istek gövdeleri
public sealed record MfaSetupVerifyRequest(string Code);
// Sprint 50: RememberDevice — "bu cihazda 30 gün tekrar sorma" (opsiyonel; null → false).
public sealed record MfaChallengeVerifyRequest(string ChallengeToken, string Code, bool? RememberDevice = null);
public sealed record MfaRecoveryCodeVerifyRequest(string ChallengeToken, string RecoveryCode);
public sealed record MfaResetRequest(Guid UserId);

/// <summary>
/// İstemciye dönen auth gövdesi. Refresh token BURADA YOKTUR; httpOnly cookie ile gider.
/// <c>MfaSetupRequired=true</c> → token KISITLI (mfa_pending); kullanıcı MFA kurmalı (Sprint 31-MFA).
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    UserDto User,
    ActiveStudioDto? ActiveStudio,
    IReadOnlyList<MembershipDto> Memberships,
    bool MfaSetupRequired = false)
{
    public static AuthResponse From(AuthResult r, bool mfaSetupRequired = false) =>
        new(r.AccessToken, r.AccessTokenExpiresAt, r.User, r.ActiveStudio, r.Memberships, mfaSetupRequired);
}

/// <summary>Login MFA-aktif kullanıcı için: tam token YOK; istemci challengeToken ile TOTP gönderir.</summary>
public sealed record MfaChallengeResponse(string ChallengeToken)
{
    public bool MfaChallengeRequired => true;
}

/// <summary>setup/start yanıtı: authenticator için otpauth URI + manuel secret.</summary>
public sealed record MfaSetupStartResponse(string OtpauthUri, string ManualKey);

/// <summary>setup/verify yanıtı: tam auth + bir kez gösterilecek recovery code'lar.</summary>
public sealed record MfaSetupCompleteResponse(AuthResponse Auth, IReadOnlyList<string> RecoveryCodes);
