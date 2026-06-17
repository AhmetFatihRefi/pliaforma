namespace Pliaforma.Api.Auth;

/// <summary>
/// Refresh token'ı httpOnly cookie olarak yönetir. Token JS'e açılmaz (XSS'e dayanıklı),
/// localStorage'a yazılmaz. Cookie yalnızca /api/auth yoluna gönderilir.
/// </summary>
public static class RefreshCookie
{
    public const string Name = "pf_refresh";
    private const string Path = "/api/auth";

    public static void Write(HttpResponse response, string rawToken, DateTime expiresUtc, bool secure)
    {
        response.Cookies.Append(Name, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero),
        });
    }

    public static void Clear(HttpResponse response, bool secure)
    {
        response.Cookies.Append(Name, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = DateTimeOffset.UnixEpoch,
        });
    }

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
