using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    NotificationChannel Channel,
    NotificationStatus Status,
    string Title,
    string Body,
    Guid? RelatedClassSessionId,
    Guid? RelatedClassBookingId,
    Guid? RelatedPaymentId,
    DateTime? SentAtUtc,
    DateTime? ReadAtUtc,
    DateTime CreatedAt)
{
    public bool IsRead => ReadAtUtc != null;
}

/// <summary>Reminder taramasının ürettiği bildirim sayıları (Sprint 28D: line ödeme hatırlatması + throttle/skip sayaçları).</summary>
public sealed record ReminderRunResultDto(
    int ClassReminders,
    int PaymentDueSoon,
    int PaymentOverdue,
    int LinePaymentDue = 0,
    int Deferred = 0,      // per-studio cap nedeniyle bu turda ertelenen öğrenci özeti sayısı
    int SkippedGuest = 0)  // UserId NULL (misafir) olduğu için atlanan line/öğrenci sayısı
{
    public int Total => ClassReminders + PaymentDueSoon + PaymentOverdue + LinePaymentDue;
}
