using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pliaforma.Persistence.Contexts;

/// <summary>
/// YALNIZ tasarım-zamanı (dotnet ef migrations) için. Runtime'da KULLANILMAZ — gerçek bağlantı DI ile gelir.
/// Bu sayede migration, çalışan Api'yi (ve kilitli bin'ini) build etmeden Persistence üzerinden üretilebilir:
///   dotnet ef migrations add &lt;Ad&gt; --project src/Pliaforma.Persistence --startup-project src/Pliaforma.Persistence
/// Buradaki bağlantı dizesi yalnız scaffolding içindir; DB'ye bağlanılmaz (migration üretimi şema modelini kullanır).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pliaforma_designtime;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options);
    }
}
