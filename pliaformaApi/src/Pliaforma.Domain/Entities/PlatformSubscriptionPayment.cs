using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Abonelik tahsilat defteri (ledger) — stüdyo↔PLATFORM ödemesi. **Öğrenci <c>Payment</c> entity'siyle
/// KARIŞTIRILMAZ** (o stüdyo↔öğrenci). Her "ödeme kaydet" bir satır yazar + aboneliğin dönemini ilerletir
/// (aynı transaction). Gateway (Sprint 14) buraya <c>Source=Gateway</c> ile kayıt atar — aynı kontrat.
/// </summary>
public class PlatformSubscriptionPayment : BaseEntity
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid PlatformSubscriptionId { get; set; }
    public PlatformSubscription? PlatformSubscription { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    /// <summary>Bu ödemenin karşıladığı dönem.</summary>
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public DateTime PaidAt { get; set; }

    public PlatformSubscriptionPaymentSource Source { get; set; } = PlatformSubscriptionPaymentSource.Manual;

    /// <summary>Kaydeden platform admin (Manual). Gateway ödemelerinde null.</summary>
    public Guid? RecordedByPlatformAdminUserId { get; set; }

    /// <summary>Gateway sağlayıcı referansı (Sprint 14).</summary>
    public string? ProviderRef { get; set; }

    public string? Notes { get; set; }
}
