using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Auth;

/// <summary>Kullanıcının temel kimlik bilgileri (parola/hash içermez).</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    GlobalRole GlobalRole,
    bool EmailVerified);

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
    IReadOnlyList<MembershipDto> Memberships);

/// <summary>/auth/me yanıtı (token üretmez).</summary>
public sealed record MeResult(
    UserDto User,
    ActiveStudioDto? ActiveStudio,
    IReadOnlyList<MembershipDto> Memberships);
