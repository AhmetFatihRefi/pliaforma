using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Domain.Enums;
using Pliaforma.Infrastructure.Services;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? throw new InvalidOperationException("Jwt ayarları bulunamadı.");

        // Fail-fast: secret yoksa veya kısaysa uygulama açılmaz (appsettings'te düz metin tutulmaz).
        if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            throw new InvalidOperationException(
                "Jwt:Secret tanımlı değil veya 32 bayttan kısa. " +
                "Geliştirme: dotnet user-secrets set \"Jwt:Secret\" \"<en az 32 karakter>\". " +
                "Üretim: Jwt__Secret ortam değişkeni.");

        services.AddHttpContextAccessor();

        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<INotificationChannelSender, LoggingNotificationSender>();
        services.AddSingleton<IReminderWorkerInfo, ReminderWorkerInfo>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = AppClaims.StudioRole,
            };
        });

        // PlatformAdmin policy: yetki global_role claim'i üzerinden kontrol edilir (studio_role/studio bağlamı
        // GEREKMEZ). Cross-tenant /api/platform/* uçları yalnızca bu policy ile açılır.
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.PlatformAdmin, policy =>
                policy.RequireClaim(AppClaims.GlobalRole, GlobalRole.PlatformAdmin.ToString()));
        });

        return services;
    }
}
