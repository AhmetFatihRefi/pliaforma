using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Dashboards;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Reports;

public sealed record InstructorsReportDto(string Month, IReadOnlyList<InstructorReportRowDto> Instructors);

/// <summary>Aylık hoca iş hacmi raporu (Owner/StudioAdmin). N+1 yerine grup sorgularıyla.</summary>
public sealed record GetInstructorsReportQuery(string? Month) : IQuery<InstructorsReportDto>;

public sealed class GetInstructorsReportHandler : IQueryHandler<GetInstructorsReportQuery, InstructorsReportDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetInstructorsReportHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<InstructorsReportDto> Handle(GetInstructorsReportQuery request, CancellationToken ct)
    {
        var sid = _currentUser.RequireStudioId();
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, sid, ct);
        var p = ReportPeriod.Resolve(request.Month, nowLocal);

        var instructors = await _db.InstructorProfiles.AsNoTracking().Where(i => i.StudioId == sid)
            .Select(i => new { i.Id, i.DisplayName, i.Rate, i.RateType }).ToListAsync(ct);

        var sessions = await _db.ClassSessions.AsNoTracking()
            .Where(s => s.StudioId == sid && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd)
            .Select(s => new { s.InstructorProfileId, s.Status, s.StartAt, s.EndAt, s.ClassTypeId, ClassTypeName = s.ClassType!.Name })
            .ToListAsync(ct);

        var bookingByInstr = (await _db.ClassBookings.AsNoTracking()
            .Where(b => b.StudioId == sid && b.Status == ClassBookingStatus.Booked
                        && b.ClassSession!.StartAt >= p.LocalStart && b.ClassSession.StartAt < p.LocalEnd)
            .Select(b => b.ClassSession!.InstructorProfileId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var presentByInstr = (await (
            from a in _db.Attendances.AsNoTracking()
            join s in _db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == sid && s.StartAt >= p.LocalStart && s.StartAt < p.LocalEnd && a.Status == AttendanceStatus.Present
            select s.InstructorProfileId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var sessionsByInstr = sessions.GroupBy(s => s.InstructorProfileId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = instructors.Select(i =>
        {
            var mySessions = sessionsByInstr.TryGetValue(i.Id, out var list) ? list : new();
            var held = mySessions.Where(s => s.Status != ClassSessionStatus.Cancelled && s.StartAt <= nowLocal).ToList();
            var heldHours = held.Sum(s => (s.EndAt - s.StartAt).TotalHours);
            var present = presentByInstr.TryGetValue(i.Id, out var pc) ? pc : 0;
            var bookings = bookingByInstr.TryGetValue(i.Id, out var bc) ? bc : 0;
            var breakdown = mySessions.GroupBy(s => new { s.ClassTypeId, s.ClassTypeName })
                .Select(g => new ClassTypeCountDto(g.Key.ClassTypeId, g.Key.ClassTypeName, g.Count()))
                .OrderByDescending(x => x.SessionCount).ToList();
            var (earning, _) = DashboardCalc.EstimateEarning(i.RateType, i.Rate, held.Count, heldHours, present);
            return new InstructorReportRowDto(i.Id, i.DisplayName, mySessions.Count, bookings, present, earning, breakdown);
        })
        .OrderByDescending(r => r.Sessions).ToList();

        return new InstructorsReportDto(p.Label, rows);
    }
}
