namespace Pliaforma.Domain.Enums;

/// <summary>Öğrencinin satın aldığı paketin yaşam döngüsü.</summary>
public enum StudentPackageStatus
{
    /// <summary>Aktif — kullanılabilir bakiye var.</summary>
    Active = 0,

    /// <summary>Bakiye tükendi (0).</summary>
    UsedUp = 1,

    /// <summary>Geçerlilik süresi doldu (bu sürümde otomatik job yok; bilgilendirici).</summary>
    Expired = 2,

    /// <summary>İptal edildi (silinmez).</summary>
    Cancelled = 3,
}

/// <summary>
/// Paket hakkı defter (ledger) hareketinin türü. Bakiye <see cref="StudentPackageStatus"/> paketin
/// CurrentCreditBalance alanıyla senkron tutulur; ledger denetim izidir. Granted/Restored pozitif,
/// Used/Revoked negatif, Adjusted işaretli (düzeltme).
/// </summary>
public enum StudentPackageTransactionType
{
    /// <summary>Paket tanımlandı/satın alındı (+).</summary>
    Granted = 0,

    /// <summary>Ders hakkı kullanıldı (−).</summary>
    Used = 1,

    /// <summary>İptal/iade ile hak geri verildi (+).</summary>
    Restored = 2,

    /// <summary>Manuel düzeltme (işaretli, +/−).</summary>
    Adjusted = 3,

    /// <summary>Hak geri alındı (−).</summary>
    Revoked = 4,
}

/// <summary>Paket hareketinin gerekçesi (izlenebilirlik).</summary>
public enum StudentPackageTransactionReason
{
    /// <summary>Paket satın alındı/tanımlandı.</summary>
    PackagePurchased = 0,

    /// <summary>Yoklama: geldi (Present) → hak düştü.</summary>
    AttendancePresent = 1,

    /// <summary>Yoklama: gelmedi (Absent) → hak düştü (staff seçimiyle).</summary>
    AttendanceAbsent = 2,

    /// <summary>Yoklama: geç iptal → hak düştü (staff seçimiyle).</summary>
    AttendanceLateCancel = 3,

    /// <summary>Manuel düzeltme.</summary>
    ManualAdjustment = 4,

    /// <summary>Yönetici düzeltmesi.</summary>
    AdminCorrection = 5,

    /// <summary>Rezervasyon iptal edildi → hak iadesi.</summary>
    BookingCancelled = 6,

    Other = 7,
}

/// <summary>
/// Rezervasyon anındaki paket uygunluk sonucu (Sprint 14). Checker ilk dört değerden birini döner;
/// booking üzerinde ayrıca <c>RequiresPayment</c> (staff override) ve <c>Resolved</c> (sonradan tüketildi)
/// saklanabilir.
/// </summary>
public enum PackageEligibilityStatus
{
    /// <summary>Uygun paket + kullanılabilir hak var.</summary>
    Eligible = 0,

    /// <summary>Öğrencinin hiç aktif/geçerli paketi yok.</summary>
    NoPackageAtAll = 1,

    /// <summary>Bu ders grubu için paket var ama kullanılabilir hak kalmamış.</summary>
    NoBalance = 2,

    /// <summary>Yalnızca başka ders grubu için paketi var.</summary>
    WrongClassType = 3,

    /// <summary>Paketsiz/yetersiz hakla (staff) yapıldı; ödeme gerekiyor.</summary>
    RequiresPayment = 4,

    /// <summary>Önce ödeme gerekiyordu; sonradan geçerli paketle yoklama tüketimiyle çözüldü.</summary>
    Resolved = 5,
}

/// <summary>Ödeme durumu. Overdue genelde DİNAMİK hesaplanır (Pending + DueDate geçmiş), kalıcı tutulmaz.</summary>
public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Overdue = 2,
    Cancelled = 3,
    Refunded = 4,
}

/// <summary>Ödeme yöntemi (manuel kayıt; online ödeme yok).</summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    BankTransfer = 2,
    Other = 3,
}
