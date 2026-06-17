using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Packages;

/// <summary>
/// Bir öğrencinin belirli bir ders grubu (ClassType) için rezervasyon yapmaya uygun paketi var mı +
/// kullanılabilir hakkı kaldı mı değerlendirir (Sprint 14). Kredi yoklamada düşer; ancak "kullanılabilir
/// hak" hesabı, kapanmamış (iptal edilmemiş + henüz paket tüketimiyle düşülmemiş) açık rezervasyonları
/// rezerve sayar → öğrenci hak sayısından fazla rezervasyon yapamaz. Bu hesap aktif transaction içinde,
/// öğrenci satırı kilitliyken çağrılmalıdır (BookingCreator kilitler).
/// </summary>
public sealed class PackageEligibilityChecker
{
    private readonly IAppDbContext _db;
    public PackageEligibilityChecker(IAppDbContext db) => _db = db;

    public async Task<PackageEligibilityResult> EvaluateAsync(
        Guid studioId, Guid studentProfileId, Guid classTypeId, DateOnly today, CancellationToken ct)
    {
        // Aktif + süresi geçmemiş paketler.
        var pkgs = await _db.StudentPackages
            .Where(p => p.StudioId == studioId && p.StudentProfileId == studentProfileId
                        && p.Status == StudentPackageStatus.Active
                        && (p.ValidUntil == null || p.ValidUntil >= today))
            .Select(p => new PkgRow(p.Id, p.Name, p.CurrentCreditBalance, p.AllowAllClassTypesSnapshot, p.ValidUntil, p.CreatedAt))
            .ToListAsync(ct);

        if (pkgs.Count == 0)
            return new PackageEligibilityResult(PackageEligibilityStatus.NoPackageAtAll, null, null, 0);

        // Kapsam kısıtlı paketlerin izinli class type id'leri.
        var restrictedIds = pkgs.Where(p => !p.AllowAll).Select(p => p.Id).ToList();
        var allowedByPackage = new Dictionary<Guid, HashSet<Guid>>();
        if (restrictedIds.Count > 0)
        {
            var rows = await _db.StudentPackageAllowedClassTypes
                .Where(a => a.StudioId == studioId && restrictedIds.Contains(a.StudentPackageId))
                .Select(a => new { a.StudentPackageId, a.ClassTypeId })
                .ToListAsync(ct);
            foreach (var g in rows.GroupBy(r => r.StudentPackageId))
                allowedByPackage[g.Key] = g.Select(x => x.ClassTypeId).ToHashSet();
        }

        bool Covers(PkgRow p) =>
            p.AllowAll || (allowedByPackage.TryGetValue(p.Id, out var set) && set.Contains(classTypeId));

        var classTypeEligible = pkgs.Where(Covers).ToList();
        if (classTypeEligible.Count == 0)
            // Paketi var ama hiçbiri bu ders grubu için geçerli değil.
            return new PackageEligibilityResult(PackageEligibilityStatus.WrongClassType, null, null, 0);

        // Kapanmamış açık rezervasyonların paket başına sayısı (rezerve edilmiş hak).
        var eligibleIds = classTypeEligible.Select(p => p.Id).ToList();
        var reservedList = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentProfileId == studentProfileId
                        && b.Status == ClassBookingStatus.Booked
                        && b.ExpectedStudentPackageId != null && eligibleIds.Contains(b.ExpectedStudentPackageId.Value)
                        && !_db.StudentPackageTransactions.Any(t =>
                               t.SourceClassBookingId == b.Id && t.Type == StudentPackageTransactionType.Used))
            .Select(b => b.ExpectedStudentPackageId!.Value)
            .ToListAsync(ct);
        var reserved = reservedList.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var candidate = classTypeEligible
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.ValidUntil,
                p.CreatedAt,
                Available = p.Balance - (reserved.TryGetValue(p.Id, out var c) ? c : 0),
            })
            .Where(p => p.Available > 0)
            .OrderBy(p => p.ValidUntil == null).ThenBy(p => p.ValidUntil).ThenBy(p => p.CreatedAt)
            .FirstOrDefault();

        if (candidate is null)
            // Bu ders grubu için paket var ama kullanılabilir hak kalmamış.
            return new PackageEligibilityResult(PackageEligibilityStatus.NoBalance, null, null, 0);

        return new PackageEligibilityResult(
            PackageEligibilityStatus.Eligible, candidate.Id, candidate.Name, candidate.Available);
    }

    private sealed record PkgRow(Guid Id, string Name, int Balance, bool AllowAll, DateOnly? ValidUntil, DateTime CreatedAt);
}

/// <summary>Uygunluk sonucu + (uygunsa) kullanılacak paket + kalan kullanılabilir hak.</summary>
public sealed record PackageEligibilityResult(
    PackageEligibilityStatus Status, Guid? ExpectedStudentPackageId, string? ExpectedPackageName, int AvailableCredit)
{
    public bool IsEligible => Status == PackageEligibilityStatus.Eligible;

    /// <summary>Self/Instructor için 409 mesajı (Eligible'da boş).</summary>
    public string Message => Status switch
    {
        PackageEligibilityStatus.NoPackageAtAll => "Bu ders için geçerli paketiniz veya kalan hakkınız yok.",
        PackageEligibilityStatus.NoBalance => "Paket hakkınız bitmiş. Yeni paket satın almanız gerekiyor.",
        PackageEligibilityStatus.WrongClassType => "Mevcut paketiniz bu ders grubu için geçerli değil.",
        _ => string.Empty,
    };
}
