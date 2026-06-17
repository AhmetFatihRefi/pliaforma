using System.Threading.RateLimiting;

namespace Pliaforma.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
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
        });

        return services;
    }
}
