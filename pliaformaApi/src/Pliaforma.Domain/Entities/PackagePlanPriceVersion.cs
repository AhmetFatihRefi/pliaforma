using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir <see cref="PackagePlan"/>'ın tarih-bazlı fiyatı. **Fiyatın gerçek kaynağı (source of truth) budur**:
/// satış anındaki geçerli (effective) fiyat = <c>EffectiveFrom &lt;= today</c> olan en güncel versiyonun
/// fiyatıdır. Gelecek-tarihli versiyon, tarihi gelince herhangi bir job olmadan otomatik geçerli olur
/// (sorgu <c>today</c>'e bakar). <see cref="PackagePlan.Price"/> yalnız legacy/görüntü cache'idir; karar
/// buna dayanmaz. Tenant-scoped (StudioId denormalize).
/// </summary>
public class PackagePlanPriceVersion : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid PackagePlanId { get; set; }
    public PackagePlan? PackagePlan { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "TRY";

    /// <summary>Bu fiyatın geçerli olmaya başladığı tarih (stüdyo-yerel gün).</summary>
    public DateOnly EffectiveFrom { get; set; }

    public Guid CreatedByUserId { get; set; }
}
