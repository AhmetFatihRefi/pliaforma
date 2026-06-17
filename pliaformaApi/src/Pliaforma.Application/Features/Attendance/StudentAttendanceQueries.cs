using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>Aylık katılım özeti hesaplayan ortak yardımcı (öğrenci self + staff paylaşır).</summary>
internal static class AttendanceSummaries
{
    /// <summary>"YYYY-MM" → [ayBaşı, sonrakiAy); geçersiz/boşsa stüdyo-yerel içinde bulunulan ay.</summary>
    public static (DateTime Start, DateTime End, string Label) MonthRange(string? month, DateTime nowLocal)
    {
        int y = nowLocal.Year, m = nowLocal.Month;
        if (!string.IsNullOrWhiteSpace(month) && month.Length == 7 && month[4] == '-'
            && int.TryParse(month[..4], out var py) && int.TryParse(month[5..], out var pm) && pm is >= 1 and <= 12)
        {
            y = py;
            m = pm;
        }
        var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return (start, start.AddMonths(1), $"{y:D4}-{m:D2}");
    }

    public static async Task<AttendanceSummaryDto> ComputeAsync(
        IAppDbContext db, Guid studioId, Guid studentProfileId, string? month, DateTime nowLocal, CancellationToken ct)
    {
        var (start, end, label) = MonthRange(month, nowLocal);

        // Yoklamaları seansın StartAt'ine (stüdyo-yerel duvar saati) göre aya filtrele.
        var rows = await (
            from a in db.Attendances
            join s in db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == studioId && a.StudentProfileId == studentProfileId
                  && s.StartAt >= start && s.StartAt < end
            select a.Status).ToListAsync(ct);

        return new AttendanceSummaryDto(
            label,
            rows.Count(s => s == AttendanceStatus.Present),
            rows.Count(s => s == AttendanceStatus.Absent),
            rows.Count(s => s == AttendanceStatus.Excused),
            rows.Count(s => s == AttendanceStatus.LateCancel),
            rows.Count);
    }
}

// ---- Öğrenci: kendi katılım geçmişi ----
public sealed record GetMyAttendanceQuery : IQuery<IReadOnlyList<MyAttendanceDto>>;

public sealed class GetMyAttendanceHandler : IQueryHandler<GetMyAttendanceQuery, IReadOnlyList<MyAttendanceDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyAttendanceHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MyAttendanceDto>> Handle(GetMyAttendanceQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(p => p.StudioId == studioId && p.UserId == userId)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return [];

        return await (
            from a in _db.Attendances
            join s in _db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == studioId && a.StudentProfileId == profileId
            orderby s.StartAt descending
            select new MyAttendanceDto(
                a.Id, s.Id, s.ClassType!.Name, s.InstructorProfile!.DisplayName,
                s.StartAt, s.EndAt, a.Status, a.MakeupCreditTransactionId != null, a.Notes))
            .ToListAsync(cancellationToken);
    }
}

// ---- Öğrenci: kendi aylık özeti ----
public sealed record GetMyAttendanceSummaryQuery(string? Month) : IQuery<AttendanceSummaryDto>;

public sealed class GetMyAttendanceSummaryHandler : IQueryHandler<GetMyAttendanceSummaryQuery, AttendanceSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetMyAttendanceSummaryHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AttendanceSummaryDto> Handle(GetMyAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        var profileId = await _db.StudentProfiles
            .Where(p => p.StudioId == studioId && p.UserId == userId)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            var (_, _, label) = AttendanceSummaries.MonthRange(request.Month, nowLocal);
            return new AttendanceSummaryDto(label, 0, 0, 0, 0, 0);
        }

        return await AttendanceSummaries.ComputeAsync(_db, studioId, profileId.Value, request.Month, nowLocal, cancellationToken);
    }
}
