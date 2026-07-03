using System.Text.Json;
using Pliaforma.Application.Common.Models;
using Pliaforma.Application.Common.Security;

namespace Pliaforma.Api.Middleware;

/// <summary>
/// Sprint 31-MFA: <c>mfa_pending</c> claim'li token (MFA kurulumu beklenen Owner/StudioAdmin) yalnız
/// MFA-setup uçlarına erişebilir; diğer tüm uçlar 403 döner → kullanıcı kurulumu tamamlamadan platforma
/// erişemez (yalnız UI değil, API düzeyi zorlama). Anonim/normal token'lar etkilenmez.
/// </summary>
public sealed class MfaPendingGuardMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/me",
        "/api/auth/logout",
        "/api/auth/mfa/setup/start",
        "/api/auth/mfa/setup/verify",
        // Sprint 53: mobil taşıma karşılıkları (setup/start taşıma-bağımsız olduğundan ortak uç kullanılır).
        "/api/auth/mobile/logout",
        "/api/auth/mobile/mfa/setup/verify",
    };

    public MfaPendingGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && user.FindFirst(AppClaims.MfaPending)?.Value == "true"
            && !Allowed.Contains(context.Request.Path.Value ?? string.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var payload = ApiResponse<object>.Fail("MFA kurulumunu tamamlayın.", null);
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        await _next(context);
    }
}
