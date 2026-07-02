using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Auth;

/// <summary>Kullanıcının temel kimlik bilgileri (parola/hash içermez).</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    GlobalRole GlobalRole,
    bool EmailVerified,
    RegistrationIntent RegistrationIntent,
    bool MfaEnabled = false);

/// <summary>Kullanıcının bir stüdyodaki üyelik özeti (stüdyo seçimi için).</summary>
public sealed record MembershipDto(
    Guid MembershipId,
    Guid StudioId,
    string StudioName,
    string StudioSlug,
    StudioRole Role,
    MembershipStatus Status);

/// <summary>Token'ın taşıdığı aktif stüdyo bağlamı.</summary>
public sealed record ActiveStudioDto(
    Guid MembershipId,
    Guid StudioId,
    string StudioName,
    string StudioSlug,
    StudioRole Role);

/// <summary>
/// Login/register/refresh/switch sonucu. RefreshToken ham değerdir; controller bunu
/// body'ye koymaz, httpOnly cookie olarak yazar.
/// </summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User,
    ActiveStudioDto? ActiveStudio,
    IReadOnlyList<MembershipDto> Memberships,
    // Sprint 46: login/refresh/switch sonrası da pilot UI doğru açılsın diye global flag burada da taşınır.
    bool PilotSimplePeopleMode = false);

/// <summary>/auth/me yanıtı (token üretmez). MFA durumu Sprint 31-MFA. PilotSimplePeopleMode Sprint 46 (global flag).</summary>
public sealed record MeResult(
    UserDto User,
    ActiveStudioDto? ActiveStudio,
    IReadOnlyList<MembershipDto> Memberships,
    bool MfaSetupRequired = false,
    int MfaRecoveryCodesRemaining = 0,
    bool PilotSimplePeopleMode = false);

/// <summary>
/// Login / register-studio / refresh sonucu — MFA dallanmasını taşır (Sprint 31-MFA).
/// <list type="bullet">
/// <item><c>MfaChallengeRequired=true</c> → <c>Auth=null</c>; istemci <c>ChallengeToken</c> ile TOTP gönderir.</item>
/// <item><c>MfaSetupRequired=true</c> → <c>Auth</c> KISITLI (mfa_pending) token; kullanıcı MFA kurmalı.</item>
/// <item>aksi halde normal tam token.</item>
/// </list>
/// </summary>
public sealed record AuthOutcome(
    AuthResult? Auth,
    bool MfaChallengeRequired,
    string? ChallengeToken,
    bool MfaSetupRequired);
