namespace Pliaforma.Infrastructure.Settings;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>İmza anahtarı. appsettings'te DÜZ METİN TUTULMAZ; dev'de user-secrets, prod'da env var.</summary>
    public string Secret { get; set; } = default!;

    public string Issuer { get; set; } = "Pliaforma";
    public string Audience { get; set; } = "Pliaforma";

    /// <summary>Access token kısa ömürlü (varsayılan 15 dk).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token ömrü (varsayılan 7 gün).</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Sprint 53 — MOBİL istemci refresh token ömrü (varsayılan 45 gün, rotasyonlu). Mobilde haftalık
    /// yeniden-login UX'i ağır olduğundan web'den uzundur; risk, reuse tespiti + OS güvenli deposu ile dengelenir.
    /// Env: <c>Jwt__MobileRefreshTokenDays</c>.
    /// </summary>
    public int MobileRefreshTokenDays { get; set; } = 45;
}
