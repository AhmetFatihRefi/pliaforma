using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Stüdyonun tanımladığı paket şablonu (ör. "4 Derslik Reformer Paketi"). Öğrenciye paket satarken/
/// tanımlarken temel alınır; satılan paket <see cref="StudentPackage"/> olarak kopyalanır (plan sonradan
/// değişse de satılan paket etkilenmez). Tenant-scoped.
/// <para>
/// <b>Ders grubu kapsamı (Sprint 14):</b> <see cref="AllowAllClassTypes"/>=true → tüm ders gruplarında
/// geçerli; false → yalnızca <see cref="ClassTypes"/> (PackagePlanClassType) kayıtlarındaki gruplar.
/// <b>Fiyat (Sprint 14):</b> geçerli fiyat <see cref="PackagePlanPriceVersion"/>'dan hesaplanır;
/// <see cref="Price"/> yalnız legacy/görüntü cache'idir (source of truth değildir).
/// </para>
/// </summary>
public class PackagePlan : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>Paketteki ders hakkı sayısı (> 0).</summary>
    public int CreditCount { get; set; }

    /// <summary>Legacy/görüntü cache'i: bugünkü efektif fiyatla en iyi-çaba senkronlanır. Satış buna güvenmez.</summary>
    public decimal Price { get; set; }

    /// <summary>Geçerlilik süresi (gün). null → süresiz.</summary>
    public int? ValidityDays { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>true → tüm ders gruplarında geçerli. false → yalnızca <see cref="ClassTypes"/> kapsamı.</summary>
    public bool AllowAllClassTypes { get; set; } = true;

    public ICollection<PackagePlanClassType> ClassTypes { get; set; } = new List<PackagePlanClassType>();
    public ICollection<PackagePlanPriceVersion> PriceVersions { get; set; } = new List<PackagePlanPriceVersion>();
}
