using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Services;

/// <summary>
/// Telafi kredisi kilidinin EF/PostgreSQL uygulaması. Öğrenci satırını <c>FOR UPDATE</c> ile kilitler;
/// kilit aktif transaction commit'e kadar tutulur → aynı öğrenci için eşzamanlı kredi tüketimleri
/// serileşir (son kredi iki kez harcanmaz). Raw SQL bu katmanda kalır.
/// </summary>
public sealed class EfMakeupCreditLock : IMakeupCreditLock
{
    private readonly AppDbContext _db;

    public EfMakeupCreditLock(AppDbContext db) => _db = db;

    public Task LockStudentAsync(Guid studioId, Guid studentProfileId, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlInterpolatedAsync($@"
            SELECT 1 FROM ""StudentProfiles""
            WHERE ""Id"" = {studentProfileId} AND ""StudioId"" = {studioId}
            FOR UPDATE", ct);
}
