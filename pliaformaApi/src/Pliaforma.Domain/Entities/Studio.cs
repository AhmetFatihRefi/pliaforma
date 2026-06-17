using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Tenant kökü. Bir stüdyonun tek bir sahibi (Owner) vardır; tüm operasyonel veriler
/// (üyelik, ders, öğrenci, rezervasyon...) StudioId ile bu kayda bağlanır.
/// </summary>
public class Studio : BaseEntity
{
    public string Name { get; set; } = default!;

    /// <summary>URL/alt alan adı için benzersiz kısa ad.</summary>
    public string Slug { get; set; } = default!;

    /// <summary>Tekrarlama/hatırlatma hesaplarının çalışacağı IANA zaman dilimi (örn. Europe/Istanbul).</summary>
    public string TimeZoneId { get; set; } = "Europe/Istanbul";

    /// <summary>Ödemelerde varsayılan para birimi (ISO 4217).</summary>
    public string Currency { get; set; } = "TRY";

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }

    /// <summary>
    /// Platform seviyesindeki durum. Yalnızca Active iken yazma yapılabilir (StudioStatusBehavior);
    /// Suspended/Cancelled stüdyo okuyabilir ama yazamaz, verisi silinmez. Platform admin yönetir.
    /// </summary>
    public StudioStatus Status { get; set; } = StudioStatus.Active;

    /// <summary>Askıya alma zamanı (UTC). Aktifken null.</summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>Askıya alma gerekçesi. Yeniden aktifleştirmede geçmiş olarak korunabilir.</summary>
    public string? SuspendedReason { get; set; }

    /// <summary>Son yeniden aktifleştirme zamanı (UTC).</summary>
    public DateTime? ReactivatedAt { get; set; }

    /// <summary>Durumu en son değiştiren platform admin kullanıcısı.</summary>
    public Guid? UpdatedByPlatformAdminUserId { get; set; }

    /// <summary>Yalnızca okuma kolaylığı: stüdyo aktif mi (Status == Active). Veritabanına yansıtılmaz.</summary>
    public bool IsActive => Status == StudioStatus.Active;

    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }

    public ICollection<StudioMembership> Memberships { get; set; } = new List<StudioMembership>();
}
