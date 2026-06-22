using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Dashboards;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Reports;

public sealed record InstructorsReportDto(string Period, IReadOnlyList<InstructorReportRowDto> Instructors);

/// <summary>
/// Hoca iş hacmi raporu (Owner/StudioAdmin) — Sprint 19: <b>parasız & operasyonel</b>. Ders atfı FİİLÎ hocaya
/// (<c>ActualInstructorProfileId ?? InstructorProfileId</c>). Ay (Month) ya da tarih aralığı (From/To) filtresi.
/// </summary>
public sealed record GetInstructorsReportQuery(string? Month, DateOnly? From = null, DateOnly? To = null)
    : IQuery<InstructorsReportDto>;

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

        // Tarih aralığı verildiyse onu, yoksa ayı kullan.
        DateTime localStart, localEnd;
        string label;
        if (request.From is { } from && request.To is { } to && to >= from)
        {
            localStart = from.ToDateTime(TimeOnly.MinValue);
            localEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue); // To dâhil
            label = $"{from:yyyy-MM-dd}..{to:yyyy-MM-dd}";
        }
        else
        {
            var p = ReportPeriod.Resolve(request.Month, nowLocal);
            localStart = p.LocalStart; localEnd = p.LocalEnd; label = p.Label;
        }

        var instructors = await _db.InstructorProfiles.AsNoTracking().Where(i => i.StudioId == sid)
            .Select(i => new { i.Id, i.DisplayName }).ToListAsync(ct);

        var sessions = await _db.ClassSessions.AsNoTracking()
            .Where(s => s.StudioId == sid && s.StartAt >= localStart && s.StartAt < localEnd)
            .Select(s => new
            {
                s.Id, s.InstructorProfileId, s.ActualInstructorProfileId, s.Status, s.StartAt, s.IsIndividual,
                s.ClassTypeId, ClassTypeName = s.ClassType!.Name,
            })
            .ToListAsync(ct);

        // Hoca kaynaklı "işlenmedi" (telafi üretmiş) iptaller: InstructorNotDelivered telafisi olan kaynak seanslar.
        var notDeliveredSessionIds = (await _db.MakeupCreditTransactions.AsNoTracking()
            .Where(t => t.StudioId == sid && t.Reason == MakeupCreditReason.InstructorNotDelivered && t.SourceClassSessionId != null)
            .Select(t => t.SourceClassSessionId!.Value)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        // Present yoklamalar (öğrenci ders verildi → distinct öğrenci), seansın fiilî hocasına atfedilir.
        var presentRows = await (
            from a in _db.Attendances.AsNoTracking()
            join s in _db.ClassSessions on a.ClassSessionId equals s.Id
            where a.StudioId == sid && s.StartAt >= localStart && s.StartAt < localEnd && a.Status == AttendanceStatus.Present
            select new { s.InstructorProfileId, s.ActualInstructorProfileId, a.StudentProfileId })
            .ToListAsync(ct);

        static Guid Eff(Guid planned, Guid? actual) => actual ?? planned;

        // Verilen (held) dersler: iptal değil + başlamış. Fiilî hocaya göre grupla.
        var held = sessions.Where(s => s.Status != ClassSessionStatus.Cancelled && s.StartAt <= nowLocal).ToList();
        var heldByInstr = held.GroupBy(s => Eff(s.InstructorProfileId, s.ActualInstructorProfileId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var distinctStudentsByInstr = presentRows
            .GroupBy(r => Eff(r.InstructorProfileId, r.ActualInstructorProfileId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentProfileId).Distinct().Count());

        var rows = instructors.Select(i =>
        {
            var mine = heldByInstr.TryGetValue(i.Id, out var list) ? list : new();
            var group = mine.Count(s => !s.IsIndividual);
            var individual = mine.Count(s => s.IsIndividual);
            var substitution = mine.Count(s => s.ActualInstructorProfileId != null && s.ActualInstructorProfileId != s.InstructorProfileId);
            var distinctStudents = distinctStudentsByInstr.TryGetValue(i.Id, out var ds) ? ds : 0;
            var breakdown = mine.GroupBy(s => new { s.ClassTypeId, s.ClassTypeName })
                .Select(g => new ClassTypeCountDto(g.Key.ClassTypeId, g.Key.ClassTypeName, g.Count()))
                .OrderByDescending(x => x.SessionCount).ToList();

            // Sprint 30 — maaş/ödeme kırılımı (atfetme: Planlanan/Yaklaşan/İşlenmedi PLANLANAN hocaya; İşlenen FİİLÎ hocaya).
            var planned = sessions.Count(s => s.InstructorProfileId == i.Id);
            var delivered = sessions.Count(s => s.Status == ClassSessionStatus.Completed && Eff(s.InstructorProfileId, s.ActualInstructorProfileId) == i.Id);
            var upcoming = sessions.Count(s => s.Status == ClassSessionStatus.Scheduled && s.StartAt > nowLocal && s.InstructorProfileId == i.Id);
            var notDelivered = sessions.Count(s => s.Status == ClassSessionStatus.Cancelled && s.InstructorProfileId == i.Id && notDeliveredSessionIds.Contains(s.Id));
            var gaveSub = sessions.Count(s => s.Status == ClassSessionStatus.Completed && s.InstructorProfileId == i.Id && s.ActualInstructorProfileId != null && s.ActualInstructorProfileId != i.Id);

            return new InstructorReportRowDto(i.Id, i.DisplayName, mine.Count, group, individual, distinctStudents, substitution,
                planned, delivered, upcoming, notDelivered, gaveSub, breakdown);
        })
        .OrderByDescending(r => r.Delivered).ThenByDescending(r => r.Planned).ToList();

        return new InstructorsReportDto(label, rows);
    }
}
