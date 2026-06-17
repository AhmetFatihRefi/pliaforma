using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Notifications;

/// <summary>
/// Bir stüdyo için hatırlatma bildirimlerini ÜRETEN idempotent çekirdek (worker + manuel trigger paylaşır).
/// DeduplicationKey ön kontrolüyle aynı olay için ikinci bildirim üretilmez. InApp kanalına teslim (log) edilir;
/// teslim hatası iş akışını bozmaz (Status=Failed). Tenant izolasyonu: yalnızca verilen studioId üzerinde çalışır.
/// </summary>
public sealed class ReminderScanner
{
    private const int ReminderWindowHours = 25;   // ~1 gün önce (dersten en fazla bu kadar önce)
    private const int DueSoonDays = 1;             // bugün veya yarın vadeli

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly INotificationChannelSender _sender;
    private readonly ILogger<ReminderScanner> _logger;

    public ReminderScanner(IAppDbContext db, IDateTime clock, INotificationChannelSender sender, ILogger<ReminderScanner> logger)
    {
        _db = db;
        _clock = clock;
        _sender = sender;
        _logger = logger;
    }

    public async Task<ReminderRunResultDto> RunForStudioAsync(Guid studioId, CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        var today = DateOnly.FromDateTime(nowLocal);

        var candidates = new List<Notification>();

        // 1) Ders hatırlatma — yaklaşan (StartAt nowLocal..+25h), Booked + Scheduled + User'lı öğrenci.
        var classRows = await (
            from b in _db.ClassBookings
            join s in _db.ClassSessions on b.ClassSessionId equals s.Id
            join sp in _db.StudentProfiles on b.StudentProfileId equals sp.Id
            where b.StudioId == studioId && b.Status == ClassBookingStatus.Booked
                  && s.Status == ClassSessionStatus.Scheduled
                  && s.StartAt > nowLocal && s.StartAt <= nowLocal.AddHours(ReminderWindowHours)
                  && sp.UserId != null
            select new { BookingId = b.Id, SessionId = s.Id, RecipientUserId = sp.UserId!.Value, ClassTypeName = s.ClassType!.Name, s.StartAt })
            .ToListAsync(ct);

        foreach (var r in classRows)
            candidates.Add(new Notification
            {
                StudioId = studioId,
                RecipientUserId = r.RecipientUserId,
                Type = NotificationType.ClassReminder,
                Title = "Ders hatırlatma",
                Body = $"{r.ClassTypeName} dersiniz {r.StartAt:yyyy-MM-dd HH:mm} başlıyor.",
                DeduplicationKey = $"class-reminder:{r.BookingId}",
                RelatedClassSessionId = r.SessionId,
                RelatedClassBookingId = r.BookingId,
                ScheduledAtUtc = nowUtc,
            });

        // 2) Ödeme vadesi yaklaşıyor — Pending + ödenmemiş + bugün/yarın vadeli + User'lı öğrenci.
        var dueSoonRows = await (
            from p in _db.Payments
            join sp in _db.StudentProfiles on p.StudentProfileId equals sp.Id
            where p.StudioId == studioId && p.Status == PaymentStatus.Pending && p.PaidAt == null
                  && p.DueDate >= today && p.DueDate <= today.AddDays(DueSoonDays)
                  && sp.UserId != null
            select new { p.Id, RecipientUserId = sp.UserId!.Value, p.Amount, p.Currency, p.DueDate })
            .ToListAsync(ct);

        foreach (var r in dueSoonRows)
            candidates.Add(new Notification
            {
                StudioId = studioId,
                RecipientUserId = r.RecipientUserId,
                Type = NotificationType.PaymentDueSoon,
                Title = "Ödeme vadesi yaklaşıyor",
                Body = $"{r.Amount:0.##} {r.Currency} ödemenizin son tarihi {r.DueDate:yyyy-MM-dd}.",
                DeduplicationKey = $"payment-due-soon:{r.Id}",
                RelatedPaymentId = r.Id,
                ScheduledAtUtc = nowUtc,
            });

        // 3) Ödeme gecikti — effective Overdue (Pending + ödenmemiş + DueDate geçmiş) + User'lı öğrenci.
        var overdueRows = await (
            from p in _db.Payments
            join sp in _db.StudentProfiles on p.StudentProfileId equals sp.Id
            where p.StudioId == studioId && p.Status == PaymentStatus.Pending && p.PaidAt == null
                  && p.DueDate < today && sp.UserId != null
            select new { p.Id, RecipientUserId = sp.UserId!.Value, p.Amount, p.Currency, p.DueDate })
            .ToListAsync(ct);

        foreach (var r in overdueRows)
            candidates.Add(new Notification
            {
                StudioId = studioId,
                RecipientUserId = r.RecipientUserId,
                Type = NotificationType.PaymentOverdue,
                Title = "Ödeme gecikti",
                Body = $"{r.Amount:0.##} {r.Currency} ödemenizin vadesi ({r.DueDate:yyyy-MM-dd}) geçti.",
                DeduplicationKey = $"payment-overdue:{r.Id}",
                RelatedPaymentId = r.Id,
                ScheduledAtUtc = nowUtc,
            });

        if (candidates.Count == 0)
            return new ReminderRunResultDto(0, 0, 0);

        // Idempotency: zaten var olan dedup anahtarlarını ele (stüdyo içinde).
        var keys = candidates.Select(c => c.DeduplicationKey).ToList();
        var existing = await _db.Notifications
            .Where(n => n.StudioId == studioId && keys.Contains(n.DeduplicationKey))
            .Select(n => n.DeduplicationKey).ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var fresh = candidates.Where(c => !existingSet.Contains(c.DeduplicationKey)).ToList();
        if (fresh.Count == 0)
            return new ReminderRunResultDto(0, 0, 0);

        foreach (var n in fresh)
            await DispatchAsync(n, nowUtc, ct);

        _db.Notifications.AddRange(fresh);
        await _db.SaveChangesAsync(ct);

        return new ReminderRunResultDto(
            fresh.Count(n => n.Type == NotificationType.ClassReminder),
            fresh.Count(n => n.Type == NotificationType.PaymentDueSoon),
            fresh.Count(n => n.Type == NotificationType.PaymentOverdue));
    }

    /// <summary>Kanala teslim (InApp = log). Hata olsa bile akış bozulmaz; Status=Failed.</summary>
    private async Task DispatchAsync(Notification n, DateTime nowUtc, CancellationToken ct)
    {
        try
        {
            var ok = await _sender.SendAsync(n, ct);
            if (ok)
            {
                n.Status = NotificationStatus.Sent;
                n.SentAtUtc = nowUtc;
            }
            else
            {
                n.Status = NotificationStatus.Failed;
                n.FailureReason = "Kanal teslim edemedi (bu sürümde yalnızca InApp/Log aktif).";
            }
        }
        catch (Exception ex)
        {
            n.Status = NotificationStatus.Failed;
            n.FailureReason = ex.Message;
            _logger.LogWarning(ex, "Bildirim teslimi başarısız: {DedupKey}", n.DeduplicationKey);
        }
    }
}
