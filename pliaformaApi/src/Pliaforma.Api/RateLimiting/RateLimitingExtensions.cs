using System.Threading.RateLimiting;

namespace Pliaforma.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string Auth = "auth";

    /// <summary>Sprint 31C-2: tarayıcı CSP ihlal raporları (POST /api/csp-report) — log flood'u sınırlar.</summary>
    public const string CspReport = "csp-report";

    /// <summary>Sprint 52: public iletişim/demo formu (POST /api/contact) — spam/flood koruması.</summary>
    public const string Contact = "contact";
}

public static class RateLimitingExtensions
{
    /// <summary>
    /// Auth endpoint'lerine IP bazlı rate limit. Limit yapılandırılabilir
    /// (RateLimiting:AuthPermitPerMinute, varsayılan 10); Development'ta yüksek tutulur ki
    /// otomatik smoke testleri throttle olmasın. Üretimde 10 kalır.
    /// </summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPermit = configuration.GetValue<int?>("RateLimiting:AuthPermitPerMinute") ?? 10;
        var cspPermit = configuration.GetValue<int?>("RateLimiting:CspReportPermitPerMinute") ?? 20;
        // Sprint 52: iletişim formu — meşru kullanıcı nadiren 1-2 kez gönderir; saatlik pencere spam botlarını keser.
        var contactPermit = configuration.GetValue<int?>("RateLimiting:ContactPermitPerHour") ?? 5;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Sprint 31C-2: CSP ihlal raporu — IP bazlı, modest limit (log flood koruması).
            options.AddPolicy(RateLimitPolicies.CspReport, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = cspPermit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Sprint 52: iletişim/demo formu — IP başına saatte contactPermit gönderim (anti-spam).
            options.AddPolicy(RateLimitPolicies.Contact, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = contactPermit,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }
}
