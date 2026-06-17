using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Platform abonelik planı kataloğu (tenant DEĞİL; platform-level). Yalnızca PlatformAdmin yönetir.
/// Dinamik: admin ekler/düzenler/fiyat günceller. Bir abonelik bu plandan fiyatı **atama/tahsilat anında
/// snapshot'lar** — plan sonradan değişse mevcut abonelik/ödeme etkilenmez (StudentPackage deseniyle aynı).
/// </summary>
public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = default!;

    /// <summary>Serbest metin kademe etiketi (ör. "Starter", "Pro").</summary>
    public string? Tier { get; set; }

    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "TRY";
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    /// <summary>Kampanya/intro fiyatı (ör. ilk 3 ay 1.990). null → intro yok.</summary>
    public decimal? IntroMonthlyPrice { get; set; }

    /// <summary>Intro fiyatının uygulanacağı dönem adedi (ör. 3 ay). null/0 → intro yok.</summary>
    public int? IntroPeriodCount { get; set; }

    /// <summary>Kurumsal/özel teklif → fiyat sabit değil, "teklif" olarak gösterilir.</summary>
    public bool IsCustomQuote { get; set; }

    /// <summary>register-studio bu plan ile 14g trial açar. Aynı anda yalnız bir aktif default olur.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public string? Description { get; set; }

    /// <summary>Gösterim amaçlı özellik listesi (JSON dizi metni). Zorlama YOK.</summary>
    public string? FeaturesJson { get; set; }

    /// <summary>Plan limitleri — bu sürümde yalnız SAKLA/GÖSTER, zorlama yok.</summary>
    public int? MaxInstructors { get; set; }
    public int? MaxStudents { get; set; }
}
