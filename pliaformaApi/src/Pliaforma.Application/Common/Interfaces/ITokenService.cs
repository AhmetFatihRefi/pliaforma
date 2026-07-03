using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Common.Interfaces;

/// <summary>Üretilen access token ve son kullanma anı.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Üretilen refresh token: ham değer (yalnızca istemciye), hash (DB'ye) ve süre.</summary>
public sealed record GeneratedRefreshToken(string RawToken, string TokenHash, DateTime ExpiresAt);

/// <summary>JWT access ve refresh token üretimi/doğrulaması. Somut uygulama Infrastructure'da.</summary>
public interface ITokenService
{
    /// <summary>
    /// Kullanıcı için kısa ömürlü access token üretir. activeMembership verilirse
    /// token'a studio_id/studio_role/membership_id claim'leri eklenir.
    /// </summary>
    AccessToken GenerateAccessToken(User user, StudioMembership? activeMembership, bool mfaPending = false);

    /// <summary>Sprint 53: ömür kanala göre — Web: Jwt:RefreshTokenDays (7), Mobile: Jwt:MobileRefreshTokenDays (45).</summary>
    GeneratedRefreshToken GenerateRefreshToken(AuthClient client = AuthClient.Web);

    /// <summary>Ham refresh token'ın DB'de aranacak hash'ini üretir (deterministik).</summary>
    string HashRefreshToken(string rawToken);
}
