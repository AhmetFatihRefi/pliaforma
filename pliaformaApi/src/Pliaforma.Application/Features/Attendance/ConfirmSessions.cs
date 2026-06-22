using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>Ders onayı çekirdeği: başlamış + Scheduled seansı Completed yapar ve işaretlenmemiş Booked
/// öğrencileri varsayılan Present işaretler (istisnalar zaten Absent/Excused/LateCancel girilmiştir).
/// İdempotent: Completed → no-op. SaveChanges ÇAĞIRMAZ.</summary>
internal static class SessionConfirmation
{
    /// <summary>İşaretlenen yeni Present sayısını döner (zaten Completed ise -1, "atlandı" anlamında).</summary>
    public static async Task<int> ConfirmAsync(
        IAppDbContext db, AttendanceMarker marker, Guid studioId, ClassSession session, Guid userId,
        StudioRole? role, CancellationToken ct)
    {
        if (session.Status == ClassSessionStatus.Completed)
            return -1; // zaten onaylı → no-op

        // İşaretlenmemiş aktif (Booked) rezervasyonlar → varsayılan Present.
        var unmarked = await db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == session.Id
                        && b.Status == ClassBookingStatus.Booked
                        && !db.Attendances.Any(a => a.ClassBookingId == b.Id))
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (unmarked.Count > 0)
        {
            var items = unmarked.Select(id => new AttendanceMarkInput(id, AttendanceStatus.Present)).ToList();
            await marker.MarkAsync(studioId, session, userId, role, items, ct);
        }

        session.Status = ClassSessionStatus.Completed;
        return unmarked.Count;
    }
}

// ---- Tek ders onayı ----
public sealed record ConfirmSessionCommand(Guid SessionId) : ICommand<SessionAttendanceDto>, IStudioWriteRequest;

public sealed class ConfirmSessionHandler : ICommandHandler<ConfirmSessionCommand, SessionAttendanceDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly AttendanceMarker _marker;

    public ConfirmSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, AttendanceMarker marker)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _marker = marker;
    }

    public async Task<SessionAttendanceDto> Handle(ConfirmSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        // Sprint 25: Owner/Admin her dersi; Instructor yalnız planlanan VEYA fiilî hocası kendisi olan dersi onaylar.
        await SchedulingAccess.EnsureCanConfirmSessionAsync(_db, _currentUser, studioId, session, cancellationToken);

        if (session.Status == ClassSessionStatus.Cancelled)
            throw new ConflictException("İptal edilmiş ders onaylanamaz.");

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        if (session.StartAt > nowLocal)
            throw new ConflictException("Henüz başlamamış ders onaylanamaz.");

        await SessionConfirmation.ConfirmAsync(_db, _marker, studioId, session, userId, _currentUser.StudioRole, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await AttendanceViews.BuildSessionViewAsync(_db, _clock, studioId, session, cancellationToken);
    }
}

// ---- Sprint 29C: ders onayını geri al (Unconfirm) ----
public sealed record UnconfirmSessionCommand(Guid SessionId) : ICommand<SessionAttendanceDto>, IStudioWriteRequest;

public sealed class UnconfirmSessionHandler : ICommandHandler<UnconfirmSessionCommand, SessionAttendanceDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public UnconfirmSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SessionAttendanceDto> Handle(UnconfirmSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        // Yetki ConfirmSession ile AYNI (Owner/Admin tümü; Instructor planlanan/fiilî hocası kendisi olan ders).
        await SchedulingAccess.EnsureCanConfirmSessionAsync(_db, _currentUser, studioId, session, cancellationToken);

        if (session.Status != ClassSessionStatus.Completed)
            throw new ConflictException("Yalnızca tamamlanmış ders geri alınabilir.");

        // Sadece status geri al; attendance kayıtları + makeup ledger'a DOKUNMA. Delivered count türetilmiş → otomatik düşer.
        session.Status = ClassSessionStatus.Scheduled;
        await _db.SaveChangesAsync(cancellationToken);

        return await AttendanceViews.BuildSessionViewAsync(_db, _clock, studioId, session, cancellationToken);
    }
}

// ---- Bugünün derslerini topluca onayla ----
public sealed record ConfirmTodayResultDto(int ConfirmedCount, int PresentMarkedCount, int SkippedFutureCount, int AlreadyCompletedCount);

public sealed record ConfirmTodaySessionsCommand : ICommand<ConfirmTodayResultDto>, IStudioWriteRequest;

public sealed class ConfirmTodaySessionsHandler : ICommandHandler<ConfirmTodaySessionsCommand, ConfirmTodayResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly AttendanceMarker _marker;

    public ConfirmTodaySessionsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, AttendanceMarker marker)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _marker = marker;
    }

    public async Task<ConfirmTodayResultDto> Handle(ConfirmTodaySessionsCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var role = _currentUser.StudioRole;

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);
        var dayStart = nowLocal.Date;
        var dayEnd = dayStart.AddDays(1);

        // Instructor ise yalnızca kendi (planlanan) seansları; Owner/Admin tümü.
        var instructorScope = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);

        var todays = await _db.ClassSessions
            .Where(s => s.StudioId == studioId && s.StartAt >= dayStart && s.StartAt < dayEnd
                        && (instructorScope == null || s.InstructorProfileId == instructorScope))
            .ToListAsync(cancellationToken);

        var skippedFuture = todays.Count(s => s.Status == ClassSessionStatus.Scheduled && s.StartAt > nowLocal);
        var alreadyCompleted = todays.Count(s => s.Status == ClassSessionStatus.Completed);

        // Başlamış (StartAt<=now) + Scheduled dersler onaylanır. Gelecek/iptal/zaten-tamamlanmış atlanır.
        var confirmable = todays
            .Where(s => s.Status == ClassSessionStatus.Scheduled && s.StartAt <= nowLocal)
            .ToList();

        var confirmed = 0;
        var presentMarked = 0;
        foreach (var session in confirmable)
        {
            var marked = await SessionConfirmation.ConfirmAsync(_db, _marker, studioId, session, userId, role, cancellationToken);
            confirmed++;
            presentMarked += Math.Max(0, marked);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new ConfirmTodayResultDto(confirmed, presentMarked, skippedFuture, alreadyCompleted);
    }
}
