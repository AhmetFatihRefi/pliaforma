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

/// <summary>Reminder taramasının ürettiği bildirim sayıları.</summary>
public sealed record ReminderRunResultDto(int ClassReminders, int PaymentDueSoon, int PaymentOverdue)
{
    public int Total => ClassReminders + PaymentDueSoon + PaymentOverdue;
}
