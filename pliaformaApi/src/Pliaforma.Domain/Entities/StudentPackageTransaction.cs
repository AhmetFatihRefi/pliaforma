using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Paket hakkı defter (ledger) hareketi: her kullanım/düzeltme ayrı kayıt. Kaynağı Attendance ya da manuel
/// işlemdir. **Soft delete edilmez**; yanlış işlem için ters hareket (Restored/Adjusted) eklenir.
/// <see cref="StudentPackage.CurrentCreditBalance"/> bu hareketlerle senkron tutulur. Tenant-scoped.
/// </summary>
public class StudentPackageTransaction : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentPackageId { get; set; }
    public StudentPackage? StudentPackage { get; set; }

    public Guid StudentProfileId { get; set; }

    /// <summary>İşaretli miktar: Granted/Restored (+), Used/Revoked (−), Adjusted (+/−). 0 olamaz.</summary>
    public int Amount { get; set; }

    public StudentPackageTransactionType Type { get; set; }
    public StudentPackageTransactionReason Reason { get; set; }

    /// <summary>Kullanımı doğuran yoklama (varsa). Aynı yoklama için ikinci Used'ı engeller.</summary>
    public Guid? SourceAttendanceId { get; set; }

    /// <summary>Kullanımı doğuran rezervasyon (varsa).</summary>
    public Guid? SourceClassBookingId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string? Notes { get; set; }
}
