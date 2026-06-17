using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir kullanıcıya yönelik bildirim. **Tenant-scoped** (StudioId) ve **kullanıcı-scoped** (RecipientUserId).
/// Bu sürümde aktif kanal InApp (kayıt uygulamada görünür) + Log; Email/Sms/WhatsApp gerçek gönderim YOK.
/// <see cref="DeduplicationKey"/> stüdyo içinde benzersizdir → aynı olay için ikinci bildirim üretilmez
/// (idempotency). RecipientUserId zorunludur — User'ı olmayan misafir öğrenci için in-app bildirim üretilmez.
/// </summary>
public class Notification : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid RecipientUserId { get; set; }
    public User? Recipient { get; set; }

    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;

    /// <summary>Stüdyo içinde benzersiz tekilleştirme anahtarı (örn. "class-reminder:{bookingId}").</summary>
    public string DeduplicationKey { get; set; } = default!;

    public Guid? RelatedClassSessionId { get; set; }
    public Guid? RelatedClassBookingId { get; set; }
    public Guid? RelatedPaymentId { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public string? FailureReason { get; set; }
}
