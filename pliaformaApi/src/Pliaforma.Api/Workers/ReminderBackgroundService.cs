using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Notifications;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Workers;

/// <summary>
/// Periyodik hatırlatma worker'ı: aktif stüdyoları dolaşır ve her biri için <see cref="ReminderScanner"/>
/// çalıştırır (idempotent). Interval appsettings'ten (`Notifications:ReminderIntervalSeconds`). Bir stüdyo
/// hata verirse loglar ve diğerlerine devam eder; uygulama çökmez. `Notifications:WorkerEnabled=false` ise
/// devre dışı (test determinizmi için Dev'de kapalı; manuel trigger endpoint'i kullanılır).
/// </summary>
public sealed class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;
    private readonly int _interStudioDelayMs;   // Sprint 28D: stüdyolar arası opsiyonel stagger (vars. 0)

    public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = config.GetValue<int?>("Notifications:ReminderIntervalSeconds") ?? 300;
        _interval = TimeSpan.FromSeconds(Math.Max(seconds, 15));
        _enabled = config.GetValue<bool?>("Notifications:WorkerEnabled") ?? true;
        _interStudioDelayMs = Math.Max(0, config.GetValue<int?>("Notifications:InterStudioDelayMs") ?? 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Reminder worker devre dışı (Notifications:WorkerEnabled=false).");
            return;
        }

        _logger.LogInformation("Reminder worker başladı; interval={Interval}s.", _interval.TotalSeconds);
        // İlk tarama için kısa gecikme (uygulama tam açılsın).
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Reminder worker turu başarısız."); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<ReminderScanner>();

        // Yalnızca aktif stüdyolar: askıya alınmış (Suspended/Cancelled) stüdyo için reminder üretilmez.
        var studioIds = await db.Studios.Where(s => s.Status == StudioStatus.Active).Select(s => s.Id).ToListAsync(ct);
        foreach (var sid in studioIds)
        {
            try
            {
                var r = await scanner.RunForStudioAsync(sid, ct);
                if (r.Total > 0 || r.Deferred > 0)
                    _logger.LogInformation("Stüdyo {StudioId}: {N} bildirim (ders {C}, due-soon {D}, overdue {O}, line {L}); ertelenen {Def}, misafir-atlanan {Sg}.",
                        sid, r.Total, r.ClassReminders, r.PaymentDueSoon, r.PaymentOverdue, r.LinePaymentDue, r.Deferred, r.SkippedGuest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stüdyo {StudioId} için reminder taraması başarısız; diğerlerine devam.", sid);
            }

            // Sprint 28D: stüdyolar arası opsiyonel stagger (ani yük yığılmasını dağıtır).
            if (_interStudioDelayMs > 0)
                try { await Task.Delay(_interStudioDelayMs, ct); } catch (TaskCanceledException) { break; }
        }
    }
}
