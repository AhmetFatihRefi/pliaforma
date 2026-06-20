using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Contracts;

// ---------------------------------------------------------------------------------------------------
// Sprint 24 — Öğrenci self-servis (SALT-OKUNUR): kendi sözleşmeleri + borç görünürlüğü.
// GÜVENLİK: parametre YOK. studioId + userId token'dan çözülür → kendi StudentProfileId bulunur → tüm
// sorgular buna scoped. Öğrenci başka studentProfileId/contractId veremez. Trimmed DTO (internal sızıntı yok).
// ---------------------------------------------------------------------------------------------------

internal static class StudentSelfResolver
{
    /// <summary>Mevcut kullanıcının bu stüdyodaki StudentProfileId'si (yoksa null).</summary>
    public static Task<Guid?> ResolveProfileIdAsync(IAppDbContext db, Guid studioId, Guid userId, CancellationToken ct) =>
        db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
}

// ---- Kendi sözleşmelerim (+ gömülü amendment özeti) ----
public sealed record GetMyContractsQuery : IQuery<IReadOnlyList<StudentContractViewDto>>;

public sealed class GetMyContractsHandler : IQueryHandler<GetMyContractsQuery, IReadOnlyList<StudentContractViewDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetMyContractsHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<StudentContractViewDto>> Handle(GetMyContractsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var profileId = await StudentSelfResolver.ResolveProfileIdAsync(_db, studioId, userId, ct);
        if (profileId is null)
            return [];

        var contracts = await _db.StudentContracts.AsNoTracking()
            .Where(c => c.StudioId == studioId && c.StudentProfileId == profileId)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new
            {
                c.Id,
                ClassTypeName = c.ClassType!.Name,
                InstructorName = c.InstructorProfile!.DisplayName,
                c.StartDate,
                c.EndDate,
                c.TotalLessonCount,
                c.Status,
                Scheduled = _db.ClassBookings.Count(b => b.StudentContractId == c.Id
                    && b.Status == ClassBookingStatus.Booked
                    && b.ClassSession!.SessionKind == ClassSessionKind.Regular),
            })
            .ToListAsync(ct);

        if (contracts.Count == 0)
            return [];

        var ids = contracts.Select(c => c.Id).ToList();
        // Trimmed amendment: CreatedByName / Previous-New total snapshot YOK.
        var amendments = await _db.ContractAmendments.AsNoTracking()
            .Where(a => a.StudioId == studioId && ids.Contains(a.StudentContractId))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.StudentContractId,
                Dto = new StudentAmendmentDto(
                    a.Kind, a.AdditionalLessonCount, a.PreviousEndDate != a.NewEndDate,
                    a.NewEndDate, a.AdditionalPrice, a.Currency, a.Description, a.CreatedAt),
            })
            .ToListAsync(ct);
        var byContract = amendments.GroupBy(a => a.StudentContractId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<StudentAmendmentDto>)g.Select(x => x.Dto).ToList());

        return contracts.Select(c => new StudentContractViewDto(
            c.Id, c.ClassTypeName, c.InstructorName, c.StartDate, c.EndDate,
            c.TotalLessonCount, c.Scheduled, c.TotalLessonCount - c.Scheduled, c.Status,
            byContract.TryGetValue(c.Id, out var a) ? a : [])).ToList();
    }
}

// ---- Kendi borç durumum (multi-currency-safe) ----
public sealed record GetMyDebtQuery : IQuery<MyDebtSummaryDto>;

public sealed class GetMyDebtHandler : IQueryHandler<GetMyDebtQuery, MyDebtSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetMyDebtHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<MyDebtSummaryDto> Handle(GetMyDebtQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var profileId = await StudentSelfResolver.ResolveProfileIdAsync(_db, studioId, userId, ct);
        if (profileId is null)
            return new MyDebtSummaryDto([], [], []);

        // Sprint 28B: LINE-bazlı borç (amendment KATILMAZ; currency bazında ayrı; cross-currency tek toplam yok).
        var d = await ContractLineQuery.ComputeDebtAsync(_db, studioId, profileId.Value, ct);
        return new MyDebtSummaryDto(d.ByCurrency, d.Lines, d.UnassignedByCurrency);
    }
}
