using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Services;

/// <summary>
/// Paket hakkı tüketiminin EF/PostgreSQL uygulaması. Atomik koşullu UPDATE ile race-safe: satır kilidi
/// alınır ve aktif transaction commit'e kadar tutulur, böylece bakiye negatife düşmez. Bakiye 0'a inince
/// Status atomik olarak UsedUp yapılır. Raw SQL bu katmanda kalır (Application sağlayıcıdan bağımsızdır).
/// </summary>
public sealed class EfStudentPackageCreditService : IStudentPackageCreditService
{
    private readonly AppDbContext _db;

    public EfStudentPackageCreditService(AppDbContext db) => _db = db;

    public async Task<bool> TryConsumeAsync(Guid studioId, Guid studentPackageId, CancellationToken ct = default)
    {
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""StudentPackages""
            SET ""CurrentCreditBalance"" = ""CurrentCreditBalance"" - 1,
                ""Status"" = CASE WHEN ""CurrentCreditBalance"" - 1 = 0 THEN 'UsedUp' ELSE ""Status"" END,
                ""UpdatedAt"" = now() AT TIME ZONE 'utc'
            WHERE ""Id"" = {studentPackageId}
              AND ""StudioId"" = {studioId}
              AND ""IsDeleted"" = false
              AND ""Status"" = 'Active'
              AND ""CurrentCreditBalance"" > 0", ct);

        return rows == 1;
    }
}
