namespace Pliaforma.Domain.Enums;

/// <summary>
/// Bir stüdyonun platform seviyesindeki durumu. Platform admin tarafından yönetilir.
/// Yazma izni yalnızca Active durumdadır (StudioStatusBehavior).
/// </summary>
public enum StudioStatus
{
    /// <summary>Normal çalışan stüdyo. Tüm okuma/yazma açık.</summary>
    Active = 0,

    /// <summary>Askıya alınmış. Okuma çalışır, yazma engellenir; veri silinmez.</summary>
    Suspended = 1,

    /// <summary>İptal edilmiş. Bu sprintte yalnızca enum olarak ayrılmıştır (Suspended gibi davranır).</summary>
    Cancelled = 2,
}
