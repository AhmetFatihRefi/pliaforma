using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir öğrenciye satılan/atanan paket. **StudentProfileId üzerinden** çalışır (misafir öğrenciye de
/// satılabilir). `CurrentCreditBalance` hızlı okuma için denormalize tutulur ve **ledger
/// (<see cref="StudentPackageTransaction"/>) ile senkron** kalır (her hareket bakiyeyi aynı transaction'da
/// günceller). Yoklamadan ders hakkı bu bakiyeden ATOMİK düşülür (race-safe). Tenant-scoped. Paket silinmez;
/// Cancelled yapılabilir.
/// </summary>
public class StudentPackage : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentProfileId { get; set; }
    public StudentProfile? StudentProfile { get; set; }

    /// <summary>Hangi plandan üretildiği (opsiyonel; serbest paket de tanımlanabilir).</summary>
    public Guid? PackagePlanId { get; set; }
    public PackagePlan? PackagePlan { get; set; }

    public string Name { get; set; } = default!;

    public int InitialCreditCount { get; set; }

    /// <summary>Kalan ders hakkı. Negatif olamaz; ledger ile senkron.</summary>
    public int CurrentCreditBalance { get; set; }

    public decimal Price { get; set; }

    public DateOnly ValidFrom { get; set; }

    /// <summary>Geçerlilik sonu (null → süresiz). Bu sürümde otomatik Expired job YOK; bilgilendirici.</summary>
    public DateOnly? ValidUntil { get; set; }

    public StudentPackageStatus Status { get; set; } = StudentPackageStatus.Active;

    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Satın alma anında plandan snapshot'lanan ders grubu kapsamı (Sprint 14). true → tüm gruplarda
    /// geçerli; false → yalnızca <see cref="AllowedClassTypes"/> kayıtları. Plan sonradan değişse de bu
    /// snapshot sabit kalır.
    /// </summary>
    public bool AllowAllClassTypesSnapshot { get; set; } = true;

    public ICollection<StudentPackageAllowedClassType> AllowedClassTypes { get; set; } = new List<StudentPackageAllowedClassType>();
}
