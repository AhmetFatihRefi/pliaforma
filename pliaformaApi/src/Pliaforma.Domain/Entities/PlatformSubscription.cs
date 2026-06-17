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

    /// <summary>Bağlı katalog planı (snapshot kaynağı). null → serbest/legacy.</summary>
    public Guid? SubscriptionPlanId { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }

    /// <summary>Plandan atama anında kopyalanan ad (snapshot).</summary>
    public string PlanName { get; set; } = "Free";

    /// <summary>Serbest metin kademe etiketi (ör. "Starter", "Pro").</summary>
    public string? Tier { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "TRY";

    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    /// <summary>Intro/kampanya fiyatı (snapshot). null → intro yok.</summary>
    public decimal? IntroMonthlyPrice { get; set; }

    /// <summary>Kalan intro dönem adedi. Her intro tahsilatında azalır; 0 → standart fiyat.</summary>
    public int? IntroPeriodsRemaining { get; set; }

    public DateOnly CurrentPeriodStart { get; set; }
    public DateOnly CurrentPeriodEnd { get; set; }

    public DateOnly? TrialEndsAt { get; set; }

    /// <summary>Owner/admin "dönem sonunda iptal" istedi. Status DEĞİŞMEZ; dönem sonuna kadar lisanslı.</summary>
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CancelRequestedAt { get; set; }

    /// <summary>Fiilen iptal anı (dönem bitiminde anlam kazanır). Kalıcı job yok; effectiveStatus DTO'da hesaplanır.</summary>
    public DateTime? CancelledAt { get; set; }

    public string? Notes { get; set; }
}
