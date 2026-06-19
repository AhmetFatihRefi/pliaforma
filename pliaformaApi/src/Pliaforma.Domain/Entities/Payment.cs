using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Stüdyo-içi **manuel** ödeme kaydı (online ödeme YOK). Bir pakete bağlı olabilir (StudentPackageId) ya da
/// paket dışı olabilir. **Payment ile Package ayrıdır:** ödeme yapılması otomatik ders hakkı düşürmez; hak
/// kullanımı Attendance/manuel ledger ile olur. Overdue genelde DİNAMİK hesaplanır (Pending + DueDate geçmiş).
/// Tenant-scoped; finansal tutarları yalnızca Owner/StudioAdmin görür (Instructor görmez).
/// </summary>
public class Payment : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentProfileId { get; set; }
    public StudentProfile? StudentProfile { get; set; }

    public Guid? StudentPackageId { get; set; }
    public StudentPackage? StudentPackage { get; set; }

    /// <summary>
    /// Sprint 23: ödemenin bağlandığı sözleşme (opsiyonel). Sözleşme-bazlı borç hesabı bu bağa dayanır.
    /// NULL → etiketsiz ödeme; hiçbir sözleşmenin borcundan düşülmez (yalnız bilgi amaçlı gösterilir).
    /// </summary>
    public Guid? StudentContractId { get; set; }
    public StudentContract? StudentContract { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";

    public DateOnly DueDate { get; set; }

    /// <summary>Ödendiği an (UTC). null → henüz ödenmedi.</summary>
    public DateTime? PaidAt { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentMethod? Method { get; set; }

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
}
