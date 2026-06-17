namespace Pliaforma.Domain.Enums;

/// <summary>
/// Platform aboneliğinin durumu (yalnızca takip amaçlı; gerçek tahsilat YOK).
/// StudioStatus ile birebir aynı olmak zorunda değildir.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Deneme süresi devam ediyor.</summary>
    Trialing = 0,

    /// <summary>Aktif abonelik.</summary>
    Active = 1,

    /// <summary>Ödeme gecikmiş (manuel işaretlenir; otomatik tahsilat yok).</summary>
    PastDue = 2,

    /// <summary>Askıya alınmış abonelik.</summary>
    Suspended = 3,

    /// <summary>İptal edilmiş abonelik.</summary>
    Cancelled = 4,
}

/// <summary>Faturalama dönemi (takip amaçlı).</summary>
public enum BillingPeriod
{
    Monthly = 0,
    Yearly = 1,
}

/// <summary>
/// Abonelik tahsilat defteri kaynağı. Öğrenci <c>Payment</c> entity'siyle KARIŞTIRILMAZ
/// (o stüdyo↔öğrenci; bu stüdyo↔platform). Gateway (Sprint 14) <c>Gateway</c> ile yazar.
/// </summary>
public enum PlatformSubscriptionPaymentSource
{
    /// <summary>Platform admin manuel kaydetti (gateway öncesi).</summary>
    Manual = 0,

    /// <summary>Ödeme geçidi webhook'u (Sprint 14).</summary>
    Gateway = 1,

    /// <summary>Düzeltme/iade kaydı.</summary>
    Adjustment = 2,
}
