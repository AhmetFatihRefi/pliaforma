using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Dashboards;

/// <summary>Hoca aylık dashboard'unu kuran ortak çekirdek (self + Owner/Admin by-id paylaşır).</summary>
internal static class InstructorDashboardBuilder
{
    public static async Task<InstructorDashboardDto> BuildAsync(
        IAppDbContext db, IDateTime clock, Guid sid, Guid instructorProfileId, string? month, CancellationToken ct)
    {
        var profile = await db.InstructorProfiles.AsNoTracking()
            .Where(i => i.Id == instructorProfileId && i.StudioId == sid)
            .Select(i => new { i.Id, i.DisplayName })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Hoca profili", instructorProfileId);

        var nowLocal = await SessionRules.StudioNowAsync(db, clock, sid, ct);
        var p = ReportPeriod.Resolve(month, nowLocal);
        var ip = profile.Id;

        var sessions = await db.ClassSessions.AsNoTracking()
            .Where(s => s.StudioId == sid && s.InstructorProfileId == ip && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd)
            .Select(s => new { s.Status, s.StartAt, s.EndAt, s.ClassTypeId, ClassTypeName = s.ClassType!.Name })
            .ToListAsync(ct);

        var sessionsThisMonth = sessions.Count;
        var cancelled = sessions.Count(s => s.Status == ClassSessionStatus.Cancelled);

        var breakdown = sessions
            .GroupBy(s => new { s.ClassTypeId, s.ClassTypeName })
            .Select(g => new ClassTypeCountDto(g.Key.ClassTypeId, g.Key.ClassTypeName, g.Count()))
            .OrderByDescending(x => x.SessionCount).ToList();

        var bookingsCount = await db.ClassBookings.AsNoTracking().CountAsync(
            b => b.StudioId == sid && b.Status == ClassBookingStatus.Booked
                 && b.ClassSession!.InstructorProfileId == ip
                 && b.ClassSession.StartAt >= p.LocalStart && b.ClassSession.StartAt < p.LocalEnd, ct);

        var attStatuses = await (
            from a in db.Attendances.AsNoTracking()
            join s in db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == sid && s.InstructorProfileId == ip && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd
            select a.Status).ToListAsync(ct);
        var att = DashboardCalc.Attendance(attStatuses);

        var totalStudentsTaught = await db.ClassBookings.AsNoTracking()
            .Where(b => b.StudioId == sid && b.Status == ClassBookingStatus.Booked
                        && b.ClassSession!.InstructorProfileId == ip
                        && b.ClassSession.StartAt >= p.LocalStart && b.ClassSession.StartAt < p.LocalEnd)
            .Select(b => b.StudentProfileId).Distinct().CountAsync(ct);

        return new InstructorDashboardDto(
            p.Label, ip, profile.DisplayName, sessionsThisMonth, cancelled, bookingsCount,
            att.PresentCount, att.AbsentCount, att.ExcusedCount, totalStudentsTaught,
            breakdown);
    }
}

// ---- Hoca self ----
public sealed record GetMyInstructorDashboardQuery(string? Month) : IQuery<InstructorDashboardDto>;

public sealed class GetMyInstructorDashboardHandler : IQueryHandler<GetMyInstructorDashboardQuery, InstructorDashboardDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetMyInstructorDashboardHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<InstructorDashboardDto> Handle(GetMyInstructorDashboardQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var ip = await _db.InstructorProfiles.AsNoTracking()
            .Where(i => i.StudioId == sid && i.UserId == userId).Select(i => (Guid?)i.Id).FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("Bu stüdyoda hoca profiliniz yok.");
        return await InstructorDashboardBuilder.BuildAsync(_db, _clock, sid, ip, request.Month, ct);
    }
}

// ---- Owner/Admin: hoca bazlı ----
public sealed record GetInstructorDashboardQuery(Guid InstructorProfileId, string? Month) : IQuery<InstructorDashboardDto>;

public sealed class GetInstructorDashboardHandler : IQueryHandler<GetInstructorDashboardQuery, InstructorDashboardDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetInstructorDashboardHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<InstructorDashboardDto> Handle(GetInstructorDashboardQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        return await InstructorDashboardBuilder.BuildAsync(_db, _clock, sid, request.InstructorProfileId, request.Month, ct);
    }
}
