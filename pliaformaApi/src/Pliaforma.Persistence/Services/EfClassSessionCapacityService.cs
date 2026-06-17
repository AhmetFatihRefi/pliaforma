using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Services;

/// <summary>
/// Kapasite servisinin EF/PostgreSQL uygulaması. Atomik koşullu UPDATE ile race-safe:
/// UPDATE sırasında satır kilidi alınır ve aktif transaction commit'e kadar tutulur, böylece
/// eşzamanlı rezervasyonlardan yalnızca biri "BookedCount &lt; Capacity" koşulunu geçer.
/// Raw SQL bu katmanda kalır (Application sağlayıcıdan bağımsızdır).
/// </summary>
public sealed class EfClassSessionCapacityService : IClassSessionCapacityService
{
    private readonly AppDbContext _db;

    public EfClassSessionCapacityService(AppDbContext db) => _db = db;

    public async Task<bool> TryReserveSeatAsync(Guid studioId, Guid classSessionId, CancellationToken ct = default)
    {
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""ClassSessions""
            SET ""BookedCount"" = ""BookedCount"" + 1, ""UpdatedAt"" = now() AT TIME ZONE 'utc'
            WHERE ""Id"" = {classSessionId}
              AND ""StudioId"" = {studioId}
              AND ""IsDeleted"" = false
              AND ""BookedCount"" < ""Capacity""", ct);

        return rows == 1;
    }

    public Task ReleaseSeatAsync(Guid studioId, Guid classSessionId, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""ClassSessions""
            SET ""BookedCount"" = GREATEST(""BookedCount"" - 1, 0), ""UpdatedAt"" = now() AT TIME ZONE 'utc'
            WHERE ""Id"" = {classSessionId}
              AND ""StudioId"" = {studioId}", ct);
}
