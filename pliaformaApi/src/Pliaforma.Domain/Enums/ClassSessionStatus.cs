namespace Pliaforma.Domain.Enums;

/// <summary>Bir ders seansının durumu.</summary>
public enum ClassSessionStatus
{
    /// <summary>Planlandı (varsayılan).</summary>
    Scheduled = 0,

    /// <summary>İptal edildi (çakışma kontrolünde dikkate alınmaz).</summary>
    Cancelled = 1,

    /// <summary>Tamamlandı (ileride kullanılacak).</summary>
    Completed = 2,
}
