using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir stüdyonun platform aboneliği — BASİT TAKİP İSKELETİ. Gerçek tahsilat/online ödeme/fatura YOKTUR;
/// yalnızca platform admin'in plan/fiyat/dönem bilgisini takip etmesi içindir. Bir stüdyonun yalnızca
/// bir (soft-delete edilmemiş) aboneliği olur. StudioId taşır ama tenant-içi kullanıcılara açık değildir;
/// yalnızca /api/platform/* uçlarından PlatformAdmin tarafından yönetilir.
/// </summary>
public class PlatformSubscription : BaseEntity
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public string PlanName { get; set; } = "Free";

    /// <summary>Serbest metin kademe etiketi (ör. "Starter", "Pro"). Bu sprintte enum değil.</summary>
    public string? Tier { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "TRY";

    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    public DateOnly CurrentPeriodStart { get; set; }
    public DateOnly CurrentPeriodEnd { get; set; }

    public DateOnly? TrialEndsAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string? Notes { get; set; }
}
