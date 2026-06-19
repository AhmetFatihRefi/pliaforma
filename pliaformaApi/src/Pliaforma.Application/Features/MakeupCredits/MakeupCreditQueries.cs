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
    public static async Task<(int Balance, MakeupCreditSummaryDto Summary, List<MakeupCreditTransactionDto> Txs)>
        BuildLedgerAsync(IAppDbContext db, Guid studioId, Guid studentProfileId, CancellationToken ct)
    {
        var raw = await db.MakeupCreditTransactions
            .Where(t => t.StudioId == studioId && t.StudentProfileId == studentProfileId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id, t.Amount, t.Type, t.Reason, t.SourceClassSessionId, t.SourceBookingId,
                t.UsedForBookingId, t.ExpiresAt, t.Notes, t.CreatedAt,
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

        var txs = raw.Select(t => new MakeupCreditTransactionDto(
            t.Id, t.Amount, t.Type, t.Reason, t.SourceClassSessionId, t.SourceBookingId, t.UsedForBookingId,
            t.ExpiresAt, t.Notes, t.CreatedAt,
            t.SourceClassSessionId is { } sid && labelMap.TryGetValue(sid, out var sl) ? sl : null,
            t.UsedForBookingId is { } ub && usedBookingSessionMap.TryGetValue(ub, out var us) && labelMap.TryGetValue(us, out var ul) ? ul : null))
            .ToList();

        return (balance, summary, txs);
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

        var (balance, summary, txs) = await MakeupCreditView.BuildLedgerAsync(_db, studioId, student.Id, cancellationToken);
        return new StudentMakeupCreditsDto(student.Id, student.FullName, balance, summary, txs);
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
            return new MyMakeupCreditsDto(0, new MakeupCreditSummaryDto(0, 0, 0), []);

        var (balance, summary, txs) = await MakeupCreditView.BuildLedgerAsync(_db, studioId, profileId.Value, cancellationToken);
        return new MyMakeupCreditsDto(balance, summary, txs);
    }
}

// ---- Staff: telafi dersine eklenebilir öğrenciler (Sprint 20 — kullanılabilir hak > 0) ----
public sealed record GetMakeupEligibleStudentsQuery : IQuery<IReadOnlyList<MakeupEligibleStudentDto>>;

public sealed class GetMakeupEligibleStudentsHandler : IQueryHandler<GetMakeupEligibleStudentsQuery, IReadOnlyList<MakeupEligibleStudentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMakeupEligibleStudentsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MakeupEligibleStudentDto>> Handle(GetMakeupEligibleStudentsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        // Pasif olmayan + kullanılabilir telafi hakkı (bakiye) > 0 öğrenciler. Tek toplu sorgu.
        var rows = await (
            from sp in _db.StudentProfiles
            where sp.StudioId == studioId && sp.Status != StudentStatus.Inactive
            let bal = _db.MakeupCreditTransactions
                .Where(t => t.StudioId == studioId && t.StudentProfileId == sp.Id)
                .Sum(t => (int?)t.Amount) ?? 0
            where bal > 0
            orderby sp.FullName
            select new MakeupEligibleStudentDto(sp.Id, sp.FullName, bal))
            .ToListAsync(cancellationToken);

        return rows;
    }
}
