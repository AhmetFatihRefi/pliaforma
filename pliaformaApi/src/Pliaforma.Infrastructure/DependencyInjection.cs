using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Common.Settings;
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

        // Sprint 31B-1 (P0-4): Production'da bilinen placeholder/zayıf örnek değerlerle açılmayı engelle.
        // Yalnız Production'da sert davranır → Development BOZULMAZ. Seed:AdminPassword boşsa admin
        // tohumlanmaz, dolayısıyla guard atlanır (gereksiz false-positive üretmez).
        if (isProduction)
            GuardAgainstPlaceholderSecrets(jwt.Secret, configuration["Seed:AdminPassword"]);

        // Sprint 31-MFA ayarları + Production fail-fast (MFA pilotta açık olmalı; anahtar gerçek olmalı).
        services.Configure<MfaSettings>(configuration.GetSection(MfaSettings.SectionName));
        var mfa = configuration.GetSection(MfaSettings.SectionName).Get<MfaSettings>() ?? new MfaSettings();
        if (isProduction)
            GuardMfaProduction(mfa);

        ConfigureEmail(services, configuration, isProduction);

        services.AddHttpContextAccessor();

        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IMfaSecretProtector, MfaSecretProtector>();
        services.AddSingleton<IMfaChallengeService, MfaChallengeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISecurityAuditLogger, SecurityAuditLogger>();
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

            // Defense-in-depth: [Authorize]/[AllowAnonymous] taşımayan her endpoint default olarak kimlik ister.
            // Mevcut controller'lar zaten korumalı; bu, ileride attribute eklenmesi unutulan yeni uçların
            // sessizce anonim açık kalmasını engeller. Anonim kalması gereken uçlar explicit [AllowAnonymous] taşır.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Production fail-fast (Sprint 31B-1 / P0-4): JWT secret veya seed admin parolası bilinen placeholder
    /// ("CHANGE_ME") ya da bilinen zayıf örnek ("Admin!2345") içeriyorsa uygulama AÇILMAZ. Seed parolası
    /// boş/null ise (admin tohumlanmaz) kontrol atlanır.
    /// </summary>
    private static void GuardAgainstPlaceholderSecrets(string jwtSecret, string? seedAdminPassword)
    {
        // Bilinen örnek/placeholder parçaları (büyük/küçük harf duyarsız, alt-dize eşleşmesi).
        string[] forbiddenFragments = { "CHANGE_ME", "Admin!2345" };

        static bool ContainsForbidden(string? value, string[] fragments)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            foreach (var fragment in fragments)
                if (value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        if (ContainsForbidden(jwtSecret, forbiddenFragments))
            throw new InvalidOperationException(
                "Jwt:Secret Production'da örnek/placeholder bir değer içeriyor (ör. 'CHANGE_ME'). " +
                "Gerçek, rastgele en az 32 baytlık bir secret kullanın (Jwt__Secret ortam değişkeni).");

        if (ContainsForbidden(seedAdminPassword, forbiddenFragments))
            throw new InvalidOperationException(
                "Seed:AdminPassword Production'da örnek/zayıf bir değer içeriyor (ör. 'CHANGE_ME', 'Admin!2345'). " +
                "Güçlü ve benzersiz bir parola kullanın (Seed__AdminPassword) veya boş bırakın (admin tohumlanmaz).");
    }

    /// <summary>
    /// Production fail-fast (Sprint 31-MFA): MFA pilotta ZORUNLU olmalı (Mfa:Enabled=true) ve geçerli bir
    /// AES anahtarı (Mfa:EncryptionKey, base64 ≥32 bayt, placeholder değil) bulunmalı. Aksi halde açılmaz.
    /// Development/test'te bu kontrol uygulanmaz (Mfa:Enabled=false ile mevcut akışlar korunur).
    /// </summary>
    private static void GuardMfaProduction(MfaSettings mfa)
    {
        if (!mfa.Enabled)
            throw new InvalidOperationException(
                "Mfa:Enabled Production'da true OLMALI (Owner/StudioAdmin MFA pilotta zorunludur). " +
                "Mfa__Enabled=true ve geçerli bir Mfa__EncryptionKey verin.");

        if (string.IsNullOrWhiteSpace(mfa.EncryptionKey)
            || mfa.EncryptionKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Mfa:EncryptionKey tanımlı değil veya placeholder ('CHANGE_ME'). Gerçek bir base64 anahtar verin " +
                "(en az 32 bayt), örn: openssl rand -base64 32.");

        byte[] keyBytes;
        try { keyBytes = Convert.FromBase64String(mfa.EncryptionKey); }
        catch (FormatException) { throw new InvalidOperationException("Mfa:EncryptionKey geçerli base64 değil."); }

        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Mfa:EncryptionKey en az 32 bayt (base64) olmalı.");
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

        // LogEmailSender doğrulama/sıfırlama linkini (ham token gömülü) loglar — prod'da token/PII sızıntısı olur.
        // appsettings.json varsayılanı Log olduğundan, env yanlış set edilse bile prod'da fail-fast (defense-in-depth).
        if (isProduction && email.SenderMode == EmailSenderMode.Log)
        {
            throw new InvalidOperationException(
                "Production'da Email:SenderMode=Log doğrulama/sıfırlama token'larını loglar (token/PII sızıntısı). " +
                "Email:SenderMode=Smtp kullanın (Email__SenderMode=Smtp env).");
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
