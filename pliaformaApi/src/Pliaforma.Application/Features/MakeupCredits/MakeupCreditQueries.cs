using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupCredits;

internal static class MakeupCreditProjection
{
    public static IQueryable<MakeupCreditTransactionDto> Project(IQueryable<Domain.Entities.MakeupCreditTransaction> q) =>
        q.Select(t => new MakeupCreditTransactionDto(
            t.Id, t.Amount, t.Type, t.Reason, t.SourceClassSessionId, t.SourceBookingId,
            t.UsedForBookingId, t.ExpiresAt, t.Notes, t.CreatedAt));
}

// ---- Staff: bir öğrencinin kredileri ----
public sealed record GetStudentMakeupCreditsQuery(Guid StudentProfileId) : IQuery<StudentMakeupCreditsDto>;

public sealed class GetStudentMakeupCreditsHandler : IQueryHandler<GetStudentMakeupCreditsQuery, StudentMakeupCreditsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;

    public GetStudentMakeupCreditsHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits)
    {
        _db = db;
        _currentUser = currentUser;
        _credits = credits;
    }

    public async Task<StudentMakeupCreditsDto> Handle(GetStudentMakeupCreditsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        await MakeupCreditAccess.EnsureCanManageAsync(_db, _currentUser, studioId, student, cancellationToken);

        var balance = await _credits.GetBalanceAsync(studioId, student.Id, cancellationToken);
        var txs = await MakeupCreditProjection
            .Project(_db.MakeupCreditTransactions
                .Where(t => t.StudioId == studioId && t.StudentProfileId == student.Id)
                .OrderByDescending(t => t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new StudentMakeupCreditsDto(student.Id, student.FullName, balance, txs);
    }
}

// ---- Student: kendi kredileri ----
public sealed record GetMyMakeupCreditsQuery : IQuery<MyMakeupCreditsDto>;

public sealed class GetMyMakeupCreditsHandler : IQueryHandler<GetMyMakeupCreditsQuery, MyMakeupCreditsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;

    public GetMyMakeupCreditsHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits)
    {
        _db = db;
        _currentUser = currentUser;
        _credits = credits;
    }

    public async Task<MyMakeupCreditsDto> Handle(GetMyMakeupCreditsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return new MyMakeupCreditsDto(0, []);

        var balance = await _credits.GetBalanceAsync(studioId, profileId.Value, cancellationToken);
        var txs = await MakeupCreditProjection
            .Project(_db.MakeupCreditTransactions
                .Where(t => t.StudioId == studioId && t.StudentProfileId == profileId)
                .OrderByDescending(t => t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new MyMakeupCreditsDto(balance, txs);
    }
}
