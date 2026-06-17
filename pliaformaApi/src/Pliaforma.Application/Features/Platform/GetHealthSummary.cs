using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Platform;

/// <summary>
/// Sistem/gözlemlenebilirlik özeti (PlatformAdmin). READ-ONLY: yalnızca sayım/yapılandırma okur, hiçbir şeyi
/// değiştirmez. Gerçek metrik altyapısı (Prometheus vb.) YOK; basit sağlık özetidir.
/// </summary>
public sealed record GetHealthSummaryQuery : IQuery<PlatformHealthSummaryDto>;

public sealed class GetHealthSummaryHandler : IQueryHandler<GetHealthSummaryQuery, PlatformHealthSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly IReminderWorkerInfo _workerInfo;

    public GetHealthSummaryHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, IReminderWorkerInfo workerInfo)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _workerInfo = workerInfo;
    }

    public async Task<PlatformHealthSummaryDto> Handle(GetHealthSummaryQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var nowUtc = _clock.UtcNow;
        var since = nowUtc.AddHours(-24);

        var statuses = await _db.Studios.AsNoTracking().Select(s => s.Status).ToListAsync(ct);
        var totalStudios = statuses.Count;
        var activeStudios = statuses.Count(s => s == StudioStatus.Active);
        var suspendedStudios = statuses.Count(s => s == StudioStatus.Suspended);

        var notificationsPending = await _db.Notifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Pending, ct);
        var notificationsFailed = await _db.Notifications.AsNoTracking().CountAsync(n => n.Status == NotificationStatus.Failed, ct);
        var sentLast24h = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.Status == NotificationStatus.Sent && n.SentAtUtc != null && n.SentAtUtc >= since, ct);
        var lastActivity = await _db.Notifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt).Select(n => (DateTime?)n.CreatedAt).FirstOrDefaultAsync(ct);

        return new PlatformHealthSummaryDto(
            DatabaseHealthy: true,
            totalStudios, activeStudios, suspendedStudios,
            notificationsPending, notificationsFailed, sentLast24h, lastActivity,
            _workerInfo.WorkerEnabled, _workerInfo.IntervalSeconds, nowUtc);
    }
}
