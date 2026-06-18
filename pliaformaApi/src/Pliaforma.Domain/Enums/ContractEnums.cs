namespace Pliaforma.Domain.Enums;

/// <summary>
/// Öğrenci–stüdyo sözleşmesinin durumu (Sprint 18a — sözleşme-tabanlı program modeli).
/// </summary>
public enum StudentContractStatus
{
    /// <summary>Yürürlükte; programa ders üretilebilir, booking bağlanabilir.</summary>
    Active = 0,

    /// <summary>Ders hakkı tamamlandı (bilgi amaçlı; otomatik geçiş ileride).</summary>
    Completed = 1,

    /// <summary>İptal edildi; yeni ders üretilemez/bağlanamaz.</summary>
    Cancelled = 2,

    /// <summary>Tarih aralığı geçti (bilgi amaçlı).</summary>
    Expired = 3,
}
