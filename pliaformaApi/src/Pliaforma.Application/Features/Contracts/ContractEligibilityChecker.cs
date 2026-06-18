using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Contracts;

/// <summary>Sözleşme uygunluk değerlendirme sonucu.</summary>
public sealed record ContractEligibilityResult(bool IsEligible, string? Message, StudentContract? Contract);

/// <summary>
/// Bir Regular rezervasyonun verilen sözleşmeden çekilip çekilemeyeceğini değerlendirir (Sprint 18a).
/// Paket "available credit" deseninin sözleşme karşılığı: sözleşme aktif + tarih aralığında + ders tipi
/// uyumlu + kullanılabilir ders hakkı > 0 (Total − bu sözleşmeye bağlı aktif Booked Regular rezervasyon).
/// </summary>
public sealed class ContractEligibilityChecker
{
    private readonly IAppDbContext _db;
    public ContractEligibilityChecker(IAppDbContext db) => _db = db;

    public async Task<ContractEligibilityResult> EvaluateAsync(
        Guid studioId, Guid contractId, Guid studentProfileId, Guid classTypeId, DateOnly sessionDate, CancellationToken ct)
    {
        var contract = await _db.StudentContracts
            .FirstOrDefaultAsync(c => c.Id == contractId && c.StudioId == studioId, ct);

        if (contract is null)
            return new(false, "Sözleşme bulunamadı.", null);
        if (contract.Status != StudentContractStatus.Active)
            return new(false, "Sözleşme aktif değil.", null);
        if (contract.StudentProfileId != studentProfileId)
            return new(false, "Sözleşme bu öğrenciye ait değil.", null);
        if (contract.ClassTypeId != classTypeId)
            return new(false, "Sözleşme bu ders grubu için geçerli değil.", null);
        if (sessionDate < contract.StartDate || sessionDate > contract.EndDate)
            return new(false, "Ders tarihi sözleşme aralığı dışında.", null);

        var reserved = await _db.ClassBookings.CountAsync(b =>
            b.StudentContractId == contractId
            && b.Status == ClassBookingStatus.Booked
            && b.ClassSession!.SessionKind == ClassSessionKind.Regular, ct);

        if (contract.TotalLessonCount - reserved <= 0)
            return new(false, "Sözleşme ders hakkı kalmadı.", null);

        return new(true, null, contract);
    }
}
