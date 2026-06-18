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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isProduction = false)
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

        ConfigureEmail(services, configuration, isProduction);

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

    /// <summary>
    /// E-posta gönderim altyapısı (Sprint 16): mod'a göre IEmailSender seçer + fail-fast. SenderMode=Smtp ise
    /// PublicOrigin/Host/FromAddress zorunlu; prod'da Disabled (override yoksa) reddedilir — güvenlik için.
    /// </summary>
    private static void ConfigureEmail(IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        var section = configuration.GetSection(EmailSettings.SectionName);
        services.Configure<EmailSettings>(section);
        var email = section.Get<EmailSettings>() ?? new EmailSettings();

        if (email.SenderMode == EmailSenderMode.Smtp
            && (string.IsNullOrWhiteSpace(email.PublicOrigin)
                || string.IsNullOrWhiteSpace(email.Smtp.Host)
                || string.IsNullOrWhiteSpace(email.FromAddress)))
        {
            throw new InvalidOperationException(
                "Email:SenderMode=Smtp iken Email:PublicOrigin, Email:Smtp:Host ve Email:FromAddress zorunludur " +
                "(Email__PublicOrigin / Email__Smtp__Host / Email__FromAddress env).");
        }

        if (isProduction && email.SenderMode == EmailSenderMode.Disabled && !email.AllowDisabledInProduction)
        {
            throw new InvalidOperationException(
                "Production'da Email:SenderMode=Disabled güvenli değildir (doğrulama/sıfırlama e-postaları gitmez). " +
                "Bilinçliyse Email:AllowDisabledInProduction=true verin; aksi halde Email:SenderMode=Smtp kullanın.");
        }

        switch (email.SenderMode)
        {
            case EmailSenderMode.Smtp:
                services.AddScoped<IEmailSender, SmtpEmailSender>();
                break;
            case EmailSenderMode.Disabled:
                services.AddScoped<IEmailSender, NullEmailSender>();
                break;
            default:
                services.AddScoped<IEmailSender, LogEmailSender>();
                break;
        }
    }
}
