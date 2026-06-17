using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;

namespace Pliaforma.Application.Features.Payments;

/// <summary>Öğrenci kendi ödeme kayıtlarını (durum + ödeme günü) görür.</summary>
public sealed record GetMyPaymentsQuery : IQuery<IReadOnlyList<PaymentDto>>;

public sealed class GetMyPaymentsHandler : IQueryHandler<GetMyPaymentsQuery, IReadOnlyList<PaymentDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    public GetMyPaymentsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    { _db = db; _currentUser = currentUser; _clock = clock; }

    public async Task<IReadOnlyList<PaymentDto>> Handle(GetMyPaymentsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profile = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => new { s.Id, s.FullName }).FirstOrDefaultAsync(ct);
        if (profile is null)
            return [];

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));
        var rows = await _db.Payments
            .Where(p => p.StudioId == studioId && p.StudentProfileId == profile.Id)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync(ct);
        return rows.Select(p => PaymentMapping.ToDto(p, profile.FullName, today)).ToList();
    }
}
