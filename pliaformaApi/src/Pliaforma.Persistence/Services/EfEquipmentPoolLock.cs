using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Services;

/// <summary>
/// Sprint 32B: Ekipman havuz kilidinin EF/PostgreSQL uygulaması. Equipment satırını <c>FOR UPDATE</c> ile kilitler;
/// kilit aktif transaction commit'e kadar tutulur → aynı havuz için eşzamanlı seans-create/update'ler serileşir
/// (son kapasite iki kez verilmez). Raw SQL bu katmanda kalır.
/// </summary>
public sealed class EfEquipmentPoolLock : IEquipmentPoolLock
{
    private readonly AppDbContext _db;

    public EfEquipmentPoolLock(AppDbContext db) => _db = db;

    public Task LockEquipmentAsync(Guid studioId, Guid equipmentId, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlInterpolatedAsync($@"
            SELECT 1 FROM ""Equipment""
            WHERE ""Id"" = {equipmentId} AND ""StudioId"" = {studioId}
            FOR UPDATE", ct);
}
