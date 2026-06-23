using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Api.Workers;

/// <summary>
/// Sprint 31D-1 — retention purge worker (ReminderBackgroundService deseni). Periyodik olarak:
/// (a) <b>SecurityAuditLog</b> 2 yıldan eski kayıtları — DB append-only trigger nedeniyle DELETE yalnız
/// transaction içinde <c>SET LOCAL app.audit_purge='on'</c> ile; (b) <b>AuthToken/RefreshToken</b> süresi
/// dolmuş/iptal + tampon kayıtları fiziksel siler (BaseEntity soft-delete'i bypass eden raw DELETE).
/// <para><b>Güvenlik kapıları:</b> <c>Enabled=false</c>→çalışmaz · <c>DryRun=true</c>→silmez, yalnız sayar/loglar ·
/// <c>AuditRetentionDays&lt;=0</c>→refuse · cutoff run başında sabit · batched (max-iter guard) · exception app'i
/// düşürmez. ILogger birincil kanıt; tur başına ≤1 <see cref="SecurityAuditEventType.AuditRetentionPurge"/> meta event
/// (PII yok). Meta event yeni (cutoff geçmişte) → aynı turda silinmez, sonsuz döngü yok.</para>
/// </summary>
public sealed class AuditRetentionPurgeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionPurgeBackgroundService> _logger;
    private readonly bool _enabled;
    private readonly bool _dryRun;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;
    private readonly int _auditRetentionDays;
    private readonly int _tokenBufferDays;

    public AuditRetentionPurgeBackgroundService(
        IServiceScopeFactory scopeFactory, ILogger<AuditRetentionPurgeBackgroundService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = config.GetValue<bool?>("AuditPurge:Enabled") ?? false;
        _dryRun = config.GetValue<bool?>("AuditPurge:DryRun") ?? true;
        var minutes = config.GetValue<int?>("AuditPurge:IntervalMinutes") ?? 1440;
        _interval = TimeSpan.FromMinutes(Math.Max(minutes, 1));
        _batchSize = Math.Clamp(config.GetValue<int?>("AuditPurge:BatchSize") ?? 5000, 100, 100_000);
        _auditRetentionDays = config.GetValue<int?>("AuditPurge:AuditRetentionDays") ?? 730;
        _tokenBufferDays = Math.Max(0, config.GetValue<int?>("AuditPurge:TokenRetentionBufferDays") ?? 7);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Audit retention purge worker devre dışı (AuditPurge:Enabled=false).");
            return;
        }

        _logger.LogInformation("Audit retention purge worker başladı; interval={Interval}dk, dryRun={DryRun}, retention={Days}gün.",
            _interval.TotalMinutes, _dryRun, _auditRetentionDays);
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Audit retention purge turu başarısız."); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        // Yeni kayıt koruması: yanlış/0 retention → tüm geçmişi silmemek için reddet.
        if (_auditRetentionDays <= 0)
        {
            _logger.LogError("Audit retention purge REDDEDILDI: AuditRetentionDays={Days} (>0 olmalı).", _auditRetentionDays);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTime>();
        var audit = scope.ServiceProvider.GetRequiredService<ISecurityAuditLogger>();

        var now = clock.UtcNow;                               // cutoff run başında SABIT
        var auditCutoff = now.AddDays(-_auditRetentionDays);
        var tokenCutoff = now.AddDays(-_tokenBufferDays);

        if (_dryRun)
        {
            var a = await ctx.SecurityAuditLogs.CountAsync(x => x.OccurredAt < auditCutoff, ct);
            var at = await ctx.AuthTokens.IgnoreQueryFilters().CountAsync(t => t.ExpiresAt < tokenCutoff, ct);
            var rt = await ctx.RefreshTokens.IgnoreQueryFilters()
                .CountAsync(t => t.ExpiresAt < tokenCutoff || (t.RevokedAt != null && t.RevokedAt < tokenCutoff), ct);
            _logger.LogInformation("[DRY-RUN] Silinecek: audit={Audit}, authToken={Auth}, refreshToken={Refresh} (auditCutoff={Cutoff:yyyy-MM-dd}).",
                a, at, rt, auditCutoff);
            await WriteMetaAsync(audit, dryRun: true, auditCutoff, a, at, rt, ct);
            return;
        }

        var auditDeleted = await PurgeAuditAsync(ctx, auditCutoff, ct);
        var authDeleted = await PurgeBatchedAsync(ctx,
            "DELETE FROM \"AuthTokens\" WHERE \"Id\" IN (SELECT \"Id\" FROM \"AuthTokens\" WHERE \"ExpiresAt\" < {0} LIMIT " + _batchSize + ")",
            tokenCutoff, ct);
        var refreshDeleted = await PurgeBatchedAsync(ctx,
            "DELETE FROM \"RefreshTokens\" WHERE \"Id\" IN (SELECT \"Id\" FROM \"RefreshTokens\" WHERE (\"ExpiresAt\" < {0} OR (\"RevokedAt\" IS NOT NULL AND \"RevokedAt\" < {0})) LIMIT " + _batchSize + ")",
            tokenCutoff, ct);

        _logger.LogInformation("Audit retention purge tamam: audit={Audit}, authToken={Auth}, refreshToken={Refresh} (auditCutoff={Cutoff:yyyy-MM-dd}).",
            auditDeleted, authDeleted, refreshDeleted, auditCutoff);
        await WriteMetaAsync(audit, dryRun: false, auditCutoff, auditDeleted, authDeleted, refreshDeleted, ct);
    }

    /// <summary>SecurityAuditLogs purge — her batch kendi kısa transaction'ında GUC bypass ile (append-only trigger).</summary>
    private async Task<long> PurgeAuditAsync(AppDbContext ctx, DateTime cutoff, CancellationToken ct)
    {
        const string sql = "DELETE FROM \"SecurityAuditLogs\" WHERE \"Id\" IN " +
                           "(SELECT \"Id\" FROM \"SecurityAuditLogs\" WHERE \"OccurredAt\" < {0} ORDER BY \"OccurredAt\" LIMIT @batch)";
        long total = 0; int affected, guard = 0;
        var batched = sql.Replace("@batch", _batchSize.ToString());
        do
        {
            await using var tx = await ctx.Database.BeginTransactionAsync(ct);
            await ctx.Database.ExecuteSqlRawAsync("SET LOCAL app.audit_purge = 'on'", ct);
            affected = await ctx.Database.ExecuteSqlRawAsync(batched, new object[] { cutoff }, ct);
            await tx.CommitAsync(ct);
            total += affected;
        } while (affected == _batchSize && ++guard < 10_000);
        return total;
    }

    /// <summary>Token tabloları — raw batched DELETE (soft-delete interceptor + query filter bypass → fiziksel).</summary>
    private async Task<long> PurgeBatchedAsync(AppDbContext ctx, string sql, DateTime cutoff, CancellationToken ct)
    {
        long total = 0; int affected, guard = 0;
        do
        {
            affected = await ctx.Database.ExecuteSqlRawAsync(sql, new object[] { cutoff }, ct);
            total += affected;
        } while (affected == _batchSize && ++guard < 10_000);
        return total;
    }

    private static Task WriteMetaAsync(ISecurityAuditLogger audit, bool dryRun, DateTime cutoff,
        long auditN, long authN, long refreshN, CancellationToken ct) =>
        audit.LogAsync(SecurityAuditEventType.AuditRetentionPurge, SecurityAuditOutcome.Success,
            detail: $"cutoff={cutoff:yyyy-MM-dd}; dryRun={dryRun}; audit={auditN}; authToken={authN}; refreshToken={refreshN}",
            cancellationToken: ct);
}
