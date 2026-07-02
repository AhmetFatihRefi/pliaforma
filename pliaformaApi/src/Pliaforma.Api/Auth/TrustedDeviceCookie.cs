namespace Pliaforma.Api.Auth;

/// <summary>
/// Sprint 50 — "Bu cihazı hatırla" güvenilir cihaz cookie'si. httpOnly + Secure + SameSite=Strict; ham opak token
/// JS'e AÇILMAZ, localStorage'a yazılmaz. Yalnızca <c>/api/auth</c> yoluna gönderilir (login + mfa verify).
/// Değer opaque random token'dır (kullanıcı/veri içermez); sunucuda yalnız SHA-256 hash'i saklanır.
/// </summary>
public static class TrustedDeviceCookie
{
    public const string Name = "pliaforma_mfa_trusted";
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
