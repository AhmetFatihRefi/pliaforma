using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Stüdyonun tanımladığı paket şablonu (ör. "4 Derslik Reformer Paketi"). Öğrenciye paket satarken/
/// tanımlarken temel alınır; satılan paket <see cref="StudentPackage"/> olarak kopyalanır (plan sonradan
/// değişse de satılan paket etkilenmez). Tenant-scoped. Bu sürümde class type kısıtı yoktur (tüm gruplarda
/// geçerli varsayılır).
/// </summary>
public class PackagePlan : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>Paketteki ders hakkı sayısı (> 0).</summary>
    public int CreditCount { get; set; }

    public decimal Price { get; set; }

    /// <summary>Geçerlilik süresi (gün). null → süresiz.</summary>
    public int? ValidityDays { get; set; }

    public bool IsActive { get; set; } = true;
}
