using Pliaforma.Application.Features.Auth;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Auth;

// İstek gövdeleri (Ip gibi sunucu-tarafı alanları client'tan AYRI tutulur).
public sealed record RegisterStudioRequest(string StudioName, string OwnerFullName, string Email, string Password, string? Phone);
// Sprint 17 Rev2: Intent (UX niyeti) opsiyonel — null → Unknown.
public sealed record RegisterRequest(string FullName, string Email, string Password, string? Phone, RegistrationIntent? Intent);
public sealed record LoginRequest(string Email, string Password);
public sealed record SwitchStudioRequest(Guid StudioId);
public sealed record RefreshRequest(Guid? StudioId);

// Sprint 16: e-posta doğrulama + şifre sıfırlama
public sealed record VerifyEmailRequest(string Token);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// İstemciye dönen auth gövdesi. Refresh token BURADA YOKTUR; httpOnly cookie ile gider.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    UserDto User,
    ActiveStudioDto? ActiveStudio,
    IReadOnlyList<MembershipDto> Memberships)
{
    public static AuthResponse From(AuthResult r) =>
        new(r.AccessToken, r.AccessTokenExpiresAt, r.User, r.ActiveStudio, r.Memberships);
}
