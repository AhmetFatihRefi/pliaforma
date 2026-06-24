using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupCredits;

/// <summary>
/// Telafi defterini bakiye + özet (kazanılan/kullanılan/kullanılabilir) + etiketli hareketlerle kurar
/// (Sprint 20). Kaynak ders ve kullanıldığı telafi dersi adları tek toplu sorguyla eklenir (N+1 yok).
/// </summary>
internal static class MakeupCreditView
{
    public static async Task<(int Balance, MakeupCreditSummaryDto Summary, List<MakeupCreditByLineDto> ByLine, List<MakeupCreditTransactionDto> Txs)>
        BuildLedgerAsync(IAppDbContext db, Guid studioId, Guid studentProfileId, CancellationToken ct)
    {
        var raw = await db.MakeupCreditTransactions
            .Where(t => t.StudioId == studioId && t.StudentProfileId == studentProfileId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id, t.Amount, t.Type, t.Reason, t.SourceClassSessionId, t.SourceBookingId,
                t.UsedForBookingId, t.ExpiresAt, t.Notes, t.CreatedAt, t.StudentContractLineId,
            })
            .ToListAsync(ct);

        var balance = raw.Sum(t => t.Amount);
        var earned = raw.Where(t => t.Amount > 0).Sum(t => t.Amount);   // Granted + Restored (toplam +)
        var used = raw.Where(t => t.Amount < 0).Sum(t => -t.Amount);    // Used + Revoked + Expired (toplam −)
        var summary = new MakeupCreditSummaryDto(earned, used, balance);

        // Etiketler: kaynak ders (SourceClassSessionId) + kullanıldığı telafi dersi (UsedForBookingId → seans).
        var sessionIds = raw.Where(t => t.SourceClassSessionId != null).Select(t => t.SourceClassSessionId!.Value).ToHashSet();
        var usedBookingIds = raw.Where(t => t.UsedForBookingId != null).Select(t => t.UsedForBookingId!.Value).Distinct().ToList();

        var usedBookingToSession = await db.ClassBookings
            .Where(b => b.StudioId == studioId && usedBookingIds.Contains(b.Id))
            .Select(b => new { b.Id, b.ClassSessionId })
            .ToListAsync(ct);
        foreach (var b in usedBookingToSession) sessionIds.Add(b.ClassSessionId);
        var usedBookingSessionMap = usedBookingToSession.ToDictionary(b => b.Id, b => b.ClassSessionId);

        var sessionLabels = await db.ClassSessions
            .Where(s => s.StudioId == studioId && sessionIds.Contains(s.Id))
            .Select(s => new { s.Id, ClassTypeName = s.ClassType!.Name, s.StartAt })
            .ToListAsync(ct);
        var labelMap = sessionLabels.ToDictionary(s => s.Id, s => $"{s.ClassTypeName} · {s.StartAt:yyyy-MM-dd HH:mm}");

        // Sprint 28C-2: line meta + line etiketi + line-bazlı pending rezerve.
        var lineIds = raw.Where(t => t.StudentContractLineId != null).Select(t => t.StudentContractLineId!.Value).Distinct().ToList();
        var lineMeta = await db.StudentContractLines
            .Where(l => l.StudioId == studioId && lineIds.Contains(l.Id))
            .Select(l => new { l.Id, l.ClassTypeId, ClassTypeName = l.ClassType!.Name, l.IsIndividual, l.StartDate, l.EndDate })
            .ToListAsync(ct);
        var lineMetaMap = lineMeta.ToDictionary(l => l.Id);
        string LineLabel(Guid id) => lineMetaMap.TryGetValue(id, out var m) ? $"{m.ClassTypeName} · {m.StartDate:yyyy-MM-dd} – {m.EndDate:yyyy-MM-dd}" : "";
        var pendingByLine = (await db.MakeupRequests
            .Where(r => r.StudioId == studioId && r.StudentContractLineId != null && lineIds.Contains(r.StudentContractLineId!.Value)
                        && r.Status == MakeupRequestStatus.Pending)
            .Select(r => r.StudentContractLineId!.Value).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var byLine = raw.Where(t => t.StudentContractLineId != null)
            .GroupBy(t => t.StudentContractLineId!.Value)
            .Where(g => lineMetaMap.ContainsKey(g.Key))
            .Select(g =>
            {
                var m = lineMetaMap[g.Key];
                var le = g.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var lu = g.Where(t => t.Amount < 0).Sum(t => -t.Amount);
                var lbal = g.Sum(t => t.Amount);
                var avail = lbal - pendingByLine.GetValueOrDefault(g.Key);
                return new MakeupCreditByLineDto(g.Key, m.ClassTypeId, m.ClassTypeName, LineLabel(g.Key),
                    le, lu, avail, m.IsIndividual, m.StartDate, m.EndDate);
            })
            .OrderBy(b => b.ClassTypeName).ToList();

        var txs = raw.Select(t => new MakeupCreditTransactionDto(
            t.Id, t.Amount, t.Type, t.Reason, t.SourceClassSessionId, t.SourceBookingId, t.UsedForBookingId,
            t.ExpiresAt, t.Notes, t.CreatedAt,
            t.SourceClassSessionId is { } sid && labelMap.TryGetValue(sid, out var sl) ? sl : null,
            t.UsedForBookingId is { } ub && usedBookingSessionMap.TryGetValue(ub, out var us) && labelMap.TryGetValue(us, out var ul) ? ul : null,
            t.StudentContractLineId,
            t.StudentContractLineId is { } lid ? LineLabel(lid) : null))
            .ToList();

        return (balance, summary, byLine, txs);
    }
}

// ---- Staff: bir öğrencinin kredileri ----
public sealed record GetStudentMakeupCreditsQuery(Guid StudentProfileId) : IQuery<StudentMakeupCreditsDto>;

public sealed class GetStudentMakeupCreditsHandler : IQueryHandler<GetStudentMakeupCreditsQuery, StudentMakeupCreditsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudentMakeupCreditsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudentMakeupCreditsDto> Handle(GetStudentMakeupCreditsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        await MakeupCreditAccess.EnsureCanManageAsync(_db, _currentUser, studioId, student, cancellationToken);

        var (balance, summary, byLine, txs) = await MakeupCreditView.BuildLedgerAsync(_db, studioId, student.Id, cancellationToken);
        return new StudentMakeupCreditsDto(student.Id, student.FullName, balance, summary, byLine, txs);
    }
}

// ---- Student: kendi kredileri (salt-okunur) ----
public sealed record GetMyMakeupCreditsQuery : IQuery<MyMakeupCreditsDto>;

public sealed class GetMyMakeupCreditsHandler : IQueryHandler<GetMyMakeupCreditsQuery, MyMakeupCreditsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyMakeupCreditsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MyMakeupCreditsDto> Handle(GetMyMakeupCreditsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return new MyMakeupCreditsDto(0, new MakeupCreditSummaryDto(0, 0, 0), [], []);

        var (balance, summary, byLine, txs) = await MakeupCreditView.BuildLedgerAsync(_db, studioId, profileId.Value, cancellationToken);
        return new MyMakeupCreditsDto(balance, summary, byLine, txs);
    }
}

// ---- Sprint 28C-2: telafiye uygun LINE listesi — ORTAK internal query (staff booking + request eligible aynı mantık) ----
internal static class MakeupEligibleLineQuery
{
    /// <summary>
    /// Active line + KULLANILABİLİR telafi hakkı > 0 (lineBalance − line-bazlı Pending talep rezervesi). Pasif öğrenci hariç.
    /// <paramref name="onlyInstructorProfileId"/> dolu → yalnız o hocanın line'ları (Instructor self). <paramref name="level"/> opsiyonel filtre.
    /// </summary>
    public static async Task<List<MakeupEligibleLineDto>> BuildAsync(
        IAppDbContext db, MakeupCreditService credits, Guid studioId,
        Guid? onlyInstructorProfileId, StudentLevel? level, CancellationToken ct)
    {
        var lines = await db.StudentContractLines
            .Where(l => l.StudioId == studioId && l.Status == StudentContractLineStatus.Active
                        && l.StudentContract!.StudentProfile!.Status != StudentStatus.Inactive
                        && (onlyInstructorProfileId == null || l.InstructorProfileId == onlyInstructorProfileId)
                        && (level == null || l.StudentContract!.StudentProfile!.Level == level))
            .Select(l => new
            {
                l.Id, l.ClassTypeId, ClassTypeName = l.ClassType!.Name,
                l.InstructorProfileId, InstructorName = l.InstructorProfile != null ? l.InstructorProfile.DisplayName : null,
                StudentProfileId = l.StudentContract!.StudentProfileId,
                StudentName = l.StudentContract!.StudentProfile!.FullName,
                Level = l.StudentContract!.StudentProfile!.Level,
                l.IsIndividual, l.StartDate, l.EndDate,
            })
            .ToListAsync(ct);

        var result = new List<MakeupEligibleLineDto>();
        foreach (var l in lines)
        {
            var avail = await credits.GetLineAvailableBalanceAsync(studioId, l.Id, ct);
            if (avail <= 0) continue;
            result.Add(new MakeupEligibleLineDto(
                l.Id, l.StudentProfileId, l.StudentName, l.ClassTypeId, l.ClassTypeName,
                l.InstructorProfileId, l.InstructorName, $"{l.ClassTypeName} · {l.StartDate:yyyy-MM-dd} – {l.EndDate:yyyy-MM-dd}",
                avail, l.Level, l.IsIndividual, l.StartDate, l.EndDate));
        }
        return result.OrderBy(r => r.StudentName).ThenBy(r => r.ClassTypeName).ToList();
    }
}

// ---- Staff: telafi dersine eklenebilir LINE'lar (Sprint 20 → 28C-2 line-based; Owner/StudioAdmin tümü) ----
public sealed record GetMakeupEligibleStudentsQuery(StudentLevel? Level = null) : IQuery<IReadOnlyList<MakeupEligibleLineDto>>;

public sealed class GetMakeupEligibleStudentsHandler : IQueryHandler<GetMakeupEligibleStudentsQuery, IReadOnlyList<MakeupEligibleLineDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;

    public GetMakeupEligibleStudentsHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits)
    {
        _db = db;
        _currentUser = currentUser;
        _credits = credits;
    }

    public async Task<IReadOnlyList<MakeupEligibleLineDto>> Handle(GetMakeupEligibleStudentsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        return await MakeupEligibleLineQuery.BuildAsync(_db, _credits, studioId, onlyInstructorProfileId: null, request.Level, cancellationToken);
    }
}
