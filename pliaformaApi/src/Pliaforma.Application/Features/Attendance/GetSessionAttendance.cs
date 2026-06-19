using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>Seans yoklama görünümünü kuran ortak yardımcı (query + mark sonrası dönüş paylaşır).</summary>
internal static class AttendanceViews
{
    public static async Task<SessionAttendanceDto> BuildSessionViewAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, ClassSession session, CancellationToken ct)
    {
        var classTypeName = await db.ClassTypes.Where(c => c.Id == session.ClassTypeId).Select(c => c.Name).FirstAsync(ct);
        var instructorName = await db.InstructorProfiles.Where(p => p.Id == session.InstructorProfileId).Select(p => p.DisplayName).FirstAsync(ct);

        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        // Sprint 19: başlamış + iptal değil → işaretlenebilir (Completed dâhil — yoklama düzeltmeye açık).
        var canMark = session.StartAt <= nowLocal && session.Status != ClassSessionStatus.Cancelled;

        // Yalnızca aktif (Booked) rezervasyonlar yoklama listesine gelir. Sprint 19: paket bilgisi yok.
        var items = await db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == session.Id && b.Status == ClassBookingStatus.Booked)
            .OrderBy(b => b.StudentProfile!.FullName)
            .Select(b => new AttendanceItemDto(
                b.Id,
                b.StudentProfileId,
                b.StudentProfile!.FullName,
                db.Attendances.Where(a => a.ClassBookingId == b.Id).Select(a => (AttendanceStatus?)a.Status).FirstOrDefault(),
                db.Attendances.Where(a => a.ClassBookingId == b.Id).Select(a => a.Notes).FirstOrDefault(),
                db.Attendances.Any(a => a.ClassBookingId == b.Id && a.MakeupCreditTransactionId != null),
                db.Attendances.Where(a => a.ClassBookingId == b.Id).Select(a => (DateTime?)a.MarkedAt).FirstOrDefault()))
            .ToListAsync(ct);

        return new SessionAttendanceDto(session.Id, classTypeName, instructorName, session.StartAt, session.EndAt,
            session.Status, canMark, items);
    }
}

/// <summary>Bir seansın yoklama listesi. Owner/StudioAdmin tüm seanslar; Instructor yalnızca kendi seansı.</summary>
public sealed record GetSessionAttendanceQuery(Guid SessionId) : IQuery<SessionAttendanceDto>;

public sealed class GetSessionAttendanceHandler : IQueryHandler<GetSessionAttendanceQuery, SessionAttendanceDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetSessionAttendanceHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SessionAttendanceDto> Handle(GetSessionAttendanceQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        // Instructor yalnızca kendi seansı (Owner/Admin tümü) → aksi 403.
        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);

        return await AttendanceViews.BuildSessionViewAsync(_db, _clock, studioId, session, cancellationToken);
    }
}
