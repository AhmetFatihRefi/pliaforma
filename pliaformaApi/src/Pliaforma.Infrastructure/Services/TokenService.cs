using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public AccessToken GenerateAccessToken(User user, StudioMembership? activeMembership, bool mfaPending = false)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(AppClaims.GlobalRole, user.GlobalRole.ToString()),
        };

        if (activeMembership is not null)
        {
            claims.Add(new Claim(AppClaims.StudioId, activeMembership.StudioId.ToString()));
            claims.Add(new Claim(AppClaims.StudioRole, activeMembership.Role.ToString()));
            claims.Add(new Claim(AppClaims.MembershipId, activeMembership.Id.ToString()));
        }

        // Sprint 31-MFA: MFA kurulumu beklenen kullanıcı → kısıtlı token (yalnız MFA setup uçları).
        if (mfaPending)
            claims.Add(new Claim(AppClaims.MfaPending, "true"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public GeneratedRefreshToken GenerateRefreshToken(AuthClient client = AuthClient.Web)
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        // Sprint 53: kanal bazlı ömür — mobil OS güvenli deposunda saklandığından daha uzun (default 45 gün).
        var days = client == AuthClient.Mobile ? _settings.MobileRefreshTokenDays : _settings.RefreshTokenDays;
        var expires = DateTime.UtcNow.AddDays(days);
        return new GeneratedRefreshToken(raw, HashRefreshToken(raw), expires);
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
