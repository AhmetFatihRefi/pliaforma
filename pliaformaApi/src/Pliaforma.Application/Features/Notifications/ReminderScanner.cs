using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Contracts;
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
    private const int LineBodyMaxRows = 8;         // çoklu line özetinde listelenecek azami satır (Body ≤ 1000)

    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly INotificationChannelSender _sender;
    private readonly ILogger<ReminderScanner> _logger;
    private readonly int _lineReminderCap;         // Sprint 28D: per-studio per-run line özeti tavanı

    public ReminderScanner(IAppDbContext db, IDateTime clock, INotificationChannelSender sender,
        ILogger<ReminderScanner> logger, IReminderWorkerInfo workerInfo)
    {
        _db = db;
        _clock = clock;
        _sender = sender;
        _logger = logger;
        _lineReminderCap = Math.Max(1, workerInfo.LineReminderMaxPerStudioPerRun);
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

        // 4) Sprint 28D — LINE ödeme hatırlatması: Active line + DueDate dolu + DueDate<=today + remaining>0.
        //    Öğrenci-bazlı HAFTALIK ÖZET (tek bildirim/öğrenci/ISO-hafta); misafir (UserId NULL) atlanır.
        var lineSummaries = await BuildLineReminderSummariesAsync(studioId, today, nowUtc, ct);
        var skippedGuest = lineSummaries.SkippedGuest;

        if (candidates.Count == 0 && lineSummaries.Items.Count == 0)
            return new ReminderRunResultDto(0, 0, 0, 0, 0, skippedGuest);

        // Idempotency: zaten var olan dedup anahtarlarını ele (stüdyo içinde) — class/payment + line özetleri birlikte.
        var keys = candidates.Select(c => c.DeduplicationKey)
            .Concat(lineSummaries.Items.Select(x => x.Notification.DeduplicationKey)).ToList();
        var existing = await _db.Notifications
            .Where(n => n.StudioId == studioId && keys.Contains(n.DeduplicationKey))
            .Select(n => n.DeduplicationKey).ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var freshOther = candidates.Where(c => !existingSet.Contains(c.DeduplicationKey)).ToList();
        // Line özetleri: en eski DueDate önce; per-studio per-run cap → fazlası ertelenir (DedupKey yazılmaz → sonraki run üretir).
        var freshLine = lineSummaries.Items
            .Where(x => !existingSet.Contains(x.Notification.DeduplicationKey))
            .OrderBy(x => x.EarliestDue).ToList();
        var deferred = Math.Max(0, freshLine.Count - _lineReminderCap);
        var cappedLine = freshLine.Take(_lineReminderCap).Select(x => x.Notification).ToList();
        if (deferred > 0)
            _logger.LogInformation("Stüdyo {StudioId}: {N} line ödeme hatırlatması cap ({Cap}) nedeniyle ertelendi.", studioId, deferred, _lineReminderCap);

        var fresh = freshOther.Concat(cappedLine).ToList();
        if (fresh.Count == 0)
            return new ReminderRunResultDto(0, 0, 0, 0, deferred, skippedGuest);

        foreach (var n in fresh)
            await DispatchAsync(n, nowUtc, ct);

        _db.Notifications.AddRange(fresh);
        await _db.SaveChangesAsync(ct);

        return new ReminderRunResultDto(
            fresh.Count(n => n.Type == NotificationType.ClassReminder),
            fresh.Count(n => n.Type == NotificationType.PaymentDueSoon),
            fresh.Count(n => n.Type == NotificationType.PaymentOverdue),
            fresh.Count(n => n.Type == NotificationType.LinePaymentDue),
            deferred, skippedGuest);
    }

    private sealed record LineSummaryItem(Guid RecipientUserId, DateOnly EarliestDue, Notification Notification);
    private sealed record LineSummaryBuild(List<LineSummaryItem> Items, int SkippedGuest);

    /// <summary>
    /// Sprint 28D: vadesi gelmiş/geçmiş + kalanı olan Active line'lardan öğrenci-bazlı haftalık özet bildirimleri kurar.
    /// remaining 28B line-debt modelinden (ContractLineQuery.BuildAsync); UserId NULL (misafir) → atlanır (SkippedGuest).
    /// </summary>
    private async Task<LineSummaryBuild> BuildLineReminderSummariesAsync(Guid studioId, DateOnly today, DateTime nowUtc, CancellationToken ct)
    {
        var dueLineDtos = await ContractLineQuery.BuildAsync(_db,
            _db.StudentContractLines.AsNoTracking().Where(l => l.StudioId == studioId
                && l.Status == StudentContractLineStatus.Active
                && l.DueDate != null && l.DueDate <= today), today, ct);
        var dueLines = dueLineDtos.Where(l => l.Remaining > 0m).ToList();
        if (dueLines.Count == 0)
            return new LineSummaryBuild([], 0);

        var studentIds = dueLines.Select(l => l.StudentProfileId).Distinct().ToList();
        var userByStudent = (await _db.StudentProfiles.AsNoTracking()
            .Where(s => s.StudioId == studioId && studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.UserId }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => s.UserId);

        var refDate = today.ToDateTime(TimeOnly.MinValue);
        var weekTag = $"{ISOWeek.GetYear(refDate)}-W{ISOWeek.GetWeekOfYear(refDate):D2}";

        var items = new List<LineSummaryItem>();
        var skippedGuest = 0;
        foreach (var g in dueLines.GroupBy(l => l.StudentProfileId))
        {
            if (!userByStudent.TryGetValue(g.Key, out var uid) || uid is null) { skippedGuest++; continue; }
            var lines = g.OrderBy(l => l.DueDate).ToList();
            var earliest = lines.Min(l => l.DueDate!.Value);

            string body;
            if (lines.Count == 1)
            {
                var l = lines[0];
                body = $"Merhaba, sistemimizde {l.ClassTypeName} paketiniz için ödenmesi beklenen {l.Remaining:0.##} {l.Currency} "
                     + "tutarında bir ödeme görünüyor. Ödemenizi yaptıysanız lütfen bu mesajı dikkate almayınız.";
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("Merhaba, sistemimizde aşağıdaki paketleriniz için ödenmesi beklenen tutarlar görünüyor:");
                foreach (var l in lines.Take(LineBodyMaxRows))
                    sb.AppendLine($"- {l.ClassTypeName} · {l.StartDate:dd.MM.yyyy}–{l.EndDate:dd.MM.yyyy} · kalan {l.Remaining:0.##} {l.Currency} · son ödeme {l.DueDate:dd.MM.yyyy}");
                if (lines.Count > LineBodyMaxRows)
                    sb.AppendLine($"- …ve {lines.Count - LineBodyMaxRows} paket daha");
                sb.AppendLine();
                sb.Append("Ödemenizi yaptıysanız lütfen bu mesajı dikkate almayınız.");
                body = sb.ToString();
            }

            items.Add(new LineSummaryItem(uid.Value, earliest, new Notification
            {
                StudioId = studioId,
                RecipientUserId = uid.Value,
                Type = NotificationType.LinePaymentDue,
                Title = "Ödeme hatırlatması",
                Body = body,
                DeduplicationKey = $"line-payment-due:{g.Key}:{weekTag}",
                ScheduledAtUtc = nowUtc,
            }));
        }
        return new LineSummaryBuild(items, skippedGuest);
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
