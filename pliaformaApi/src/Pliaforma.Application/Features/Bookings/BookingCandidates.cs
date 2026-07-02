using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Contracts;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;
using ValidationException = Pliaforma.Application.Common.Exceptions.ValidationException;

namespace Pliaforma.Application.Features.Bookings;

// =====================================================================================================
// Sprint 29B — Aday öğrenci seçimi. Yetkili manuel açtığı bir seansa eklenebilecek uygun öğrencileri
// (Regular: tercih gün/saat sıralı; Makeup: telafi hakkı bazlı, gün-bağımsız) döner. YALNIZ OKUMA.
//   Auth: Owner/StudioAdmin (controller). Yanlış session kind → 400 (bug gizlenmesin).
// =====================================================================================================

/// <summary>Regular seansa eklenebilir aday (öğrenci + uygun line). Aynı öğrencinin çok uygun line'ı → çok satır.</summary>
public sealed record RegularCandidateDto(
    Guid StudentProfileId,
    string StudentName,
    StudentLevel? StudentLevel,
    Guid LineId,
    string LineLabel,
    string ClassTypeName,
    int AvailableForPlanning,
    bool IsIndividual,
    bool MatchedDay,
    bool HasPreferredSlots,
    TimeOnly? NearestPreferredTime,
    int? MinutesFromSession);

public sealed record GetRegularBookingCandidatesQuery(Guid ClassSessionId, StudentLevel? Level)
    : IQuery<IReadOnlyList<RegularCandidateDto>>;

public sealed class GetRegularBookingCandidatesHandler
    : IQueryHandler<GetRegularBookingCandidatesQuery, IReadOnlyList<RegularCandidateDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetRegularBookingCandidatesHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<RegularCandidateDto>> Handle(GetRegularBookingCandidatesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.ClassSessionId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders seansı", request.ClassSessionId);
        if (session.SessionKind != ClassSessionKind.Regular)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["sessionKind"] = new[] { "Bu uç yalnız normal (Regular) seanslar içindir." },
            });

        var sessionDate = DateOnly.FromDateTime(session.StartAt);
        var sessionDay = session.StartAt.DayOfWeek;
        var sessionTime = TimeOnly.FromDateTime(session.StartAt);

        // Aday line'lar (DB filtresi): Active + classType + instructor (28C-1) + tarih-aralığı + öğrenci aktif + level.
        // Sprint 47: gizli pilot line (IsPilot) aday listesinde görünmez — staff'a "100000 hak" gibi anlamsız satır sunulmaz.
        var lines = await _db.StudentContractLines
            .Include(l => l.StudentContract!).ThenInclude(c => c.StudentProfile)
            .Include(l => l.ClassType)
            .Where(l => l.StudioId == studioId
                        && l.Status == StudentContractLineStatus.Active
                        && !l.IsPilot
                        && l.ClassTypeId == session.ClassTypeId
                        // Sprint 33: line hocası null → herhangi bir hoca; dolu → eşleşme zorunlu.
                        && (l.InstructorProfileId == null || l.InstructorProfileId == session.InstructorProfileId)
                        && l.StartDate <= sessionDate && sessionDate <= l.EndDate
                        && l.StudentContract!.StudentProfile!.Status != StudentStatus.Inactive
                        && (request.Level == null || l.StudentContract!.StudentProfile!.Level == request.Level))
            .ToListAsync(ct);

        if (lines.Count == 0) return System.Array.Empty<RegularCandidateDto>();

        var lineIds = lines.Select(l => l.Id).ToList();
        var slots = await _db.StudentContractLinePreferredSlots.AsNoTracking()
            .Where(s => lineIds.Contains(s.StudentContractLineId) && s.IsActive)
            .Select(s => new { s.StudentContractLineId, s.DayOfWeek, s.PreferredStartTime })
            .ToListAsync(ct);
        var slotsByLine = slots.GroupBy(s => s.StudentContractLineId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // (dto, sortGroup, minutesKey) — sortGroup 0: gün-eşleşen timed, 1: gün-eşleşen saatsiz, 2: slotsuz legacy.
        var staged = new List<(RegularCandidateDto Dto, int Group, int Minutes)>();
        foreach (var l in lines)
        {
            var spId = l.StudentContract!.StudentProfileId;
            // Sprint 33: kapasite tercihi (HARD). Tercih varsa yalnız kapasitesi ≤ tercih olan dersler aday (bireysel=1 özel hâl).
            if (l.PreferredMaxCapacity is { } cap && session.Capacity > cap) continue;
            // Planlanabilir hak > 0 (AvailableForPlanning = LessonCount − Booked Regular).
            var avail = await ContractLineRules.AvailableAsync(_db, l, ct);
            if (avail <= 0) continue;
            // Aynı-saat çakışma (bu seansta zaten Booked dahil — kendi seansı pencereyle örtüşür) + pending telafi çakışması.
            var ids = new[] { spId };
            if (await SessionRules.StudentConflictAsync(_db, studioId, ids, session.StartAt, session.EndAt, System.Array.Empty<Guid>(), ct))
                continue;
            if (await SessionRules.PendingStudentConflictAsync(_db, studioId, ids, session.StartAt, session.EndAt, null, ct))
                continue;

            var hasSlots = slotsByLine.TryGetValue(l.Id, out var lineSlots) && lineSlots.Count > 0;
            int group;
            bool matchedDay;
            TimeOnly? nearest = null;
            int? minutes = null;
            if (hasSlots)
            {
                var sameDay = lineSlots!.Where(s => s.DayOfWeek == sessionDay).ToList();
                if (sameDay.Count == 0) continue; // slotu var ama gün yok → ele (hard filter)
                matchedDay = true;
                var timed = sameDay.Where(s => s.PreferredStartTime is not null)
                    .Select(s => new { Time = s.PreferredStartTime!.Value, Diff = MinutesBetween(s.PreferredStartTime!.Value, sessionTime) })
                    .OrderBy(x => x.Diff).ToList();
                if (timed.Count > 0)
                {
                    group = 0; nearest = timed[0].Time; minutes = timed[0].Diff;
                }
                else
                {
                    group = 1; // yalnız saatsiz slot o günde
                }
            }
            else
            {
                group = 2; matchedDay = false; // slotsuz legacy → en sonda
            }

            var label = $"{l.ClassType!.Name} · {l.StartDate:yyyy-MM-dd} – {l.EndDate:yyyy-MM-dd}";
            var dto = new RegularCandidateDto(
                spId, l.StudentContract!.StudentProfile!.FullName, l.StudentContract!.StudentProfile!.Level,
                l.Id, label, l.ClassType!.Name, avail, l.IsIndividual, matchedDay, hasSlots, nearest, minutes);
            staged.Add((dto, group, minutes ?? 0));
        }

        // Sıralama: grup (0<1<2) → grup0 dakika yakınlığı → öğrenci adı.
        return staged
            .OrderBy(x => x.Group)
            .ThenBy(x => x.Minutes)
            .ThenBy(x => x.Dto.StudentName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Dto)
            .ToList();
    }

    private static int MinutesBetween(TimeOnly a, TimeOnly b) =>
        (int)System.Math.Abs((a.ToTimeSpan() - b.ToTimeSpan()).TotalMinutes);
}

// ---- Makeup adayları: session-scoped (classType + çakışma); tercih gün/saat UYGULANMAZ ----
public sealed record GetMakeupBookingCandidatesQuery(Guid ClassSessionId, StudentLevel? Level)
    : IQuery<IReadOnlyList<MakeupEligibleLineDto>>;

public sealed class GetMakeupBookingCandidatesHandler
    : IQueryHandler<GetMakeupBookingCandidatesQuery, IReadOnlyList<MakeupEligibleLineDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;
    public GetMakeupBookingCandidatesHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits)
    { _db = db; _currentUser = currentUser; _credits = credits; }

    public async Task<IReadOnlyList<MakeupEligibleLineDto>> Handle(GetMakeupBookingCandidatesQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.ClassSessionId && s.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ders seansı", request.ClassSessionId);
        if (session.SessionKind != ClassSessionKind.Makeup)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["sessionKind"] = new[] { "Bu uç yalnız telafi (Makeup) seanslar içindir." },
            });

        // Mevcut line-bazlı telafi-uygun query (Active + makeup available>0 + level) + session-scope.
        var eligible = await MakeupEligibleLineQuery.BuildAsync(_db, _credits, studioId, onlyInstructorProfileId: null, request.Level, ct);

        var result = new List<MakeupEligibleLineDto>();
        foreach (var e in eligible)
        {
            if (e.ClassTypeId != session.ClassTypeId) continue; // classType server-side eşleşmesi
            var ids = new[] { e.StudentProfileId };
            if (await SessionRules.StudentConflictAsync(_db, studioId, ids, session.StartAt, session.EndAt, System.Array.Empty<Guid>(), ct))
                continue;
            if (await SessionRules.PendingStudentConflictAsync(_db, studioId, ids, session.StartAt, session.EndAt, null, ct))
                continue;
            result.Add(e);
        }
        return result;
    }
}
