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

/// <summary>
/// Sözleşme ek işlemi (amendment) kategorisi (Sprint 23). Raporlama/filtreleme için yardımcı alandır;
/// asıl doğruluk delta alanlarındadır (AdditionalLessonCount, Previous/NewEndDate, AdditionalPrice).
/// </summary>
public enum ContractAmendmentKind
{
    /// <summary>Ek ders hakkı (genelde ek ücretle birlikte).</summary>
    ExtraLessons = 0,

    /// <summary>Yalnız bitiş tarihi ileri alma (ders hakkı artmaz).</summary>
    DateExtension = 1,

    /// <summary>Yalnız ücret düzeltme/ek ücret.</summary>
    PriceAdjustment = 2,

    /// <summary>Birden çok deltayı birleştiren işlem (ders + tarih + ücret).</summary>
    Mixed = 3,

    /// <summary>Önceki bir işlemi geri alan/düzelten ters kayıt (negatif delta olabilir).</summary>
    Correction = 4,

    /// <summary>Diğer/özel anlaşma.</summary>
    Other = 5,
}
