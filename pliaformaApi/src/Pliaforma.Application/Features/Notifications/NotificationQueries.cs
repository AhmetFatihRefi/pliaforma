using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Notifications;

// ---- Kullanıcının kendi bildirimleri (aktif stüdyo) ----
public sealed record GetMyNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>>;

public sealed class GetMyNotificationsHandler : IQueryHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetMyNotificationsHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        if (_currentUser.StudioId is not { } sid)
            return [];

        return await _db.Notifications.AsNoTracking()
            .Where(n => n.StudioId == sid && n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt).Take(50)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Channel, n.Status, n.Title, n.Body,
                n.RelatedClassSessionId, n.RelatedClassBookingId, n.RelatedPaymentId,
                n.SentAtUtc, n.ReadAtUtc, n.CreatedAt))
            .ToListAsync(ct);
    }
}

// ---- Okunmamış sayısı ----
public sealed record GetUnreadCountQuery : IQuery<int>;

public sealed class GetUnreadCountHandler : IQueryHandler<GetUnreadCountQuery, int>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetUnreadCountHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        if (_currentUser.StudioId is not { } sid)
            return 0;
        return await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.StudioId == sid && n.RecipientUserId == userId && n.ReadAtUtc == null, ct);
    }
}

// ---- Okundu işaretle (idempotent) ----
public sealed record MarkNotificationReadCommand(Guid Id) : ICommand<Unit>;

public sealed class MarkNotificationReadHandler : ICommandHandler<MarkNotificationReadCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public MarkNotificationReadHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<Unit> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var sid = _currentUser.RequireStudioId();

        // Yalnızca kendi bildirimi (başka kullanıcınınki/başka stüdyo → 404).
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.StudioId == sid && x.RecipientUserId == userId, ct)
            ?? throw new NotFoundException("Bildirim", request.Id);

        if (n.ReadAtUtc == null)
        {
            n.ReadAtUtc = _clock.UtcNow;
            n.Status = NotificationStatus.Read;
            await _db.SaveChangesAsync(ct);
        }
        return Unit.Value;
    }
}

// ---- Tümünü okundu yap ----
public sealed record MarkAllNotificationsReadCommand : ICommand<Unit>;

public sealed class MarkAllNotificationsReadHandler : ICommandHandler<MarkAllNotificationsReadCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public MarkAllNotificationsReadHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<Unit> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var sid = _currentUser.RequireStudioId();
        var now = _clock.UtcNow;

        var unread = await _db.Notifications
            .Where(n => n.StudioId == sid && n.RecipientUserId == userId && n.ReadAtUtc == null)
            .ToListAsync(ct);
        foreach (var n in unread)
        {
            n.ReadAtUtc = now;
            n.Status = NotificationStatus.Read;
        }
        if (unread.Count > 0)
            await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
