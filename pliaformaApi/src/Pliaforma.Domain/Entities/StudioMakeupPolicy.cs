using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Stüdyonun telafi kredisi politikası (stüdyo başına bir kayıt). Kayıt yoksa varsayılanlar geçerlidir;
/// ilk güncellemede oluşturulur. Tenant-scoped.
/// </summary>
public class StudioMakeupPolicy : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    /// <summary>Bir öğrenciye bir takvim ayında manuel verilebilecek azami kredi (stüdyo/hoca iptalleri hariç).</summary>
    public int MonthlyManualCreditLimit { get; set; } = 2;

    /// <summary>Verilen kredinin varsayılan geçerlilik süresi (gün). Bu sürümde bilgilendirici; bakiyede zorlanmaz.</summary>
    public int DefaultCreditValidityDays { get; set; } = 30;

    /// <summary>Instructor'ın manuel kredi vermesine izin verilsin mi?</summary>
    public bool AllowInstructorManualGrant { get; set; } = true;

    /// <summary>Owner/StudioAdmin, kredisi olmayan öğrenci için telafi rezervasyonu yapabilsin mi?</summary>
    public bool AllowMakeupBookingWithoutCreditByAdmin { get; set; }

    /// <summary>Politika kaydı yokken kullanılacak bellek-içi varsayılan (persist edilmez).</summary>
    public static StudioMakeupPolicy Default(Guid studioId) => new()
    {
        StudioId = studioId,
        MonthlyManualCreditLimit = 2,
        DefaultCreditValidityDays = 30,
        AllowInstructorManualGrant = true,
        AllowMakeupBookingWithoutCreditByAdmin = false,
    };
}
