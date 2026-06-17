using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Api.Setup;

public static class DbInitializer
{
    /// <summary>
    /// Migration'ları uygular ve (yapılandırılmışsa) bir PlatformAdmin tohumlar. İdempotenttir.
    /// Hatalar YUTULMAZ — başarısızlıkta uygulama açılmaz (fail-fast).
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<Program>>();

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Veritabanı migration'ları uygulandı.");

        await SeedAdminAsync(sp, db, logger);
    }

    private static async Task SeedAdminAsync(IServiceProvider sp, AppDbContext db, ILogger logger)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Seed:AdminEmail/Password verilmedi; admin tohumlama atlandı.");
            return;
        }

        var normalized = EmailNormalizer.Normalize(email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized))
            return;

        var hasher = sp.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new User
        {
            Email = email.Trim(),
            NormalizedEmail = normalized,
            PasswordHash = hasher.Hash(password),
            FullName = "Platform Admin",
            GlobalRole = GlobalRole.PlatformAdmin,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("PlatformAdmin tohumlandı: {Email}", email);
    }
}
