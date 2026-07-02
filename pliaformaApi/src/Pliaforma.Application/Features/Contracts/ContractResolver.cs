using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Contracts;

/// <summary>
/// Sprint 18b: bir Regular rezervasyon için **uygun aktif sözleşmeyi otomatik bulur** (staff sözleşme seçmediyse).
/// Kurallar: aynı öğrenci + aynı ders tipi + seans tarihi sözleşme aralığında + Status=Active + kullanılabilir ders
/// hakkı > 0. Belirleyici (deterministik) seçim: **en erken biten** (EndDate), eşitse en eski oluşturulan (CreatedAt).
/// </summary>
public sealed class ContractResolver
{
    private readonly IAppDbContext _db;
    public ContractResolver(IAppDbContext db) => _db = db;

    public async Task<StudentContract?> ResolveAsync(
        Guid studioId, Guid studentProfileId, Guid classTypeId, DateOnly sessionDate, CancellationToken ct)
    {
        var candidates = await _db.StudentContracts
            .Where(c => c.StudioId == studioId
                        && c.StudentProfileId == studentProfileId
                        && c.ClassTypeId == classTypeId
                        && c.Status == StudentContractStatus.Active
                        // Sprint 48: gizli pilot header (IsPilot) asla otomatik seçilmez (savunma-derinliği; bu
                        // resolver şu an hiçbir handler'a inject edilmiyor ama gelecekte sızıntıyı önler).
                        && !c.IsPilot
                        && c.StartDate <= sessionDate && sessionDate <= c.EndDate)
            .OrderBy(c => c.EndDate).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);

        foreach (var c in candidates)
        {
            var reserved = await _db.ClassBookings.CountAsync(b =>
                b.StudentContractId == c.Id
                && b.Status == ClassBookingStatus.Booked
                && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);
            if (c.TotalLessonCount - reserved > 0)
                return c;
        }
        return null;
    }
}
