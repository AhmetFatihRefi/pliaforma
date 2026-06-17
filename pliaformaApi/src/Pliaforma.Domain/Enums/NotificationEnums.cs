namespace Pliaforma.Domain.Enums;

/// <summary>Bildirim türü (hangi olaydan doğdu).</summary>
public enum NotificationType
{
    ClassReminder = 0,
    PaymentDueSoon = 1,
    PaymentOverdue = 2,
    MakeupCreditGranted = 3,
    BookingCancelled = 4,
    SessionRescheduled = 5,
    System = 6,
}

/// <summary>
/// Bildirim kanalı. Bu sürümde AKTİF kanal yalnızca InApp (ve Log). Email/Sms/WhatsApp enum olarak vardır
/// ama gerçek gönderim YAPILMAZ (no-op/log).
/// </summary>
public enum NotificationChannel
{
    InApp = 0,
    Log = 1,
    Email = 2,
    Sms = 3,
    WhatsApp = 4,
}

/// <summary>Bildirim teslim durumu.</summary>
public enum NotificationStatus
{
    /// <summary>Oluşturuldu, henüz dağıtılmadı.</summary>
    Pending = 0,

    /// <summary>Kanala teslim edildi (InApp = kayıt görünür; Log = loglandı).</summary>
    Sent = 1,

    /// <summary>Teslim başarısız (FailureReason dolu).</summary>
    Failed = 2,

    /// <summary>Kullanıcı okudu.</summary>
    Read = 3,
}
