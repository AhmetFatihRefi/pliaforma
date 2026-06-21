using Microsoft.Extensions.Configuration;
using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Infrastructure.Services;

/// <summary>Worker yapılandırmasını appsettings'ten okur (worker ile aynı anahtarlar/defaultlar).</summary>
public sealed class ReminderWorkerInfo : IReminderWorkerInfo
{
    public bool WorkerEnabled { get; }
    public int IntervalSeconds { get; }
    public int LineReminderMaxPerStudioPerRun { get; }

    public ReminderWorkerInfo(IConfiguration config)
    {
        WorkerEnabled = config.GetValue<bool?>("Notifications:WorkerEnabled") ?? true;
        IntervalSeconds = Math.Max(config.GetValue<int?>("Notifications:ReminderIntervalSeconds") ?? 300, 15);
        LineReminderMaxPerStudioPerRun = Math.Max(1, config.GetValue<int?>("Notifications:LineReminderMaxPerStudioPerRun") ?? 50);
    }
}
