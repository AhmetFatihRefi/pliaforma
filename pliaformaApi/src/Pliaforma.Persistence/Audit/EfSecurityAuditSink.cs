using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Audit;

/// <summary>
/// Denetim kaydını YENİ bir <see cref="AppDbContext"/> (ayrı bağlantı/işlem) ile yazar → ana iş transaction'ı
/// rollback olsa bile (ör. başarısız login 401 fırlatınca) denetim kaydı KALICI olur. Best-effort (try/catch).
/// </summary>
public sealed class EfSecurityAuditSink : ISecurityAuditSink
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ILogger<EfSecurityAuditSink> _logger;

    public EfSecurityAuditSink(DbContextOptions<AppDbContext> options, ILogger<EfSecurityAuditSink> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task WriteAsync(SecurityAuditLog entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var ctx = new AppDbContext(_options);
            ctx.SecurityAuditLogs.Add(entry);
            await ctx.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: denetim yazımı asla kullanıcı akışını kırmaz. PII/secret loglanmaz; yalnız olay türü.
            _logger.LogError(ex, "SecurityAuditLog yazılamadı (event={EventType})", entry.EventType);
        }
    }
}
