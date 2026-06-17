using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Features.Packages;

/// <summary>
/// Paket planı fiyat versiyonlama kuralları (Sprint 14). Geçerli (effective) fiyat = <c>EffectiveFrom
/// &lt;= today</c> olan en güncel versiyon. Gelecek-tarihli zam, tarihi gelince job olmadan otomatik
/// geçerli olur. <see cref="PackagePlan.Price"/> yalnız cache'tir; bu kurallar versiyon tablosunu kullanır.
/// </summary>
internal static class PackagePricing
{
    /// <summary>Bugün geçerli versiyon (yoksa null).</summary>
    public static PackagePlanPriceVersion? CurrentVersion(IEnumerable<PackagePlanPriceVersion> versions, DateOnly today) =>
        versions.Where(v => v.EffectiveFrom <= today)
            .OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.CreatedAt)
            .FirstOrDefault();

    /// <summary>Gelecekteki ilk (en yakın) versiyon (yoksa null).</summary>
    public static PackagePlanPriceVersion? NextVersion(IEnumerable<PackagePlanPriceVersion> versions, DateOnly today) =>
        versions.Where(v => v.EffectiveFrom > today)
            .OrderBy(v => v.EffectiveFrom).ThenBy(v => v.CreatedAt)
            .FirstOrDefault();
}
