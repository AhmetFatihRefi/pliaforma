using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Infrastructure.Services;

/// <summary>
/// MFA challenge için stateless kısa-ömürlü imzalı token. <b>Audience ayrıdır</b>
/// (<see cref="ChallengeAudience"/>) → normal JwtBearer (aud=Pliaforma) bunu full access olarak KABUL ETMEZ.
/// Yalnız MFA verify uçlarında doğrulanır.
/// </summary>
public sealed class MfaChallengeService : IMfaChallengeService
{
    public const string ChallengeAudience = "pliaforma-mfa-challenge";
    public const string Purpose = "mfa_challenge";

    private readonly JwtSettings _jwt;
    private readonly MfaSettings _mfa;

    public MfaChallengeService(IOptions<JwtSettings> jwt, IOptions<MfaSettings> mfa)
    {
        _jwt = jwt.Value;
        _mfa = mfa.Value;
    }

    public string CreateChallengeToken(Guid userId)
    {
        var expires = DateTime.UtcNow.AddMinutes(_mfa.ChallengeTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("purpose", Purpose),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_jwt.Issuer, ChallengeAudience, claims, expires: expires, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateChallengeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            // MapInboundClaims=false: "sub" claim'i nameidentifier'a REMAP edilmesin (aksi halde sub okunamaz).
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = ChallengeAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1),
            }, out _);

            if (principal.FindFirst("purpose")?.Value != Purpose)
                return null;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
