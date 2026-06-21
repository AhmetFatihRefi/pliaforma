using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.MakeupRequests;

// ---- Talep açmak için uygun LINE'lar (Sprint 28C-2): KULLANILABİLİR line telafi hakkı > 0. Instructor → yalnız
// KENDİ instructorProfileId'siyle eşleşen aktif/kredili line'lar; Owner/StudioAdmin → tümü. Opsiyonel level filtre. ----
public sealed record GetMakeupRequestEligibleStudentsQuery(StudentLevel? Level = null) : IQuery<IReadOnlyList<MakeupEligibleLineDto>>;

public sealed class GetMakeupRequestEligibleStudentsHandler : IQueryHandler<GetMakeupRequestEligibleStudentsQuery, IReadOnlyList<MakeupEligibleLineDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly MakeupCreditService _credits;

    public GetMakeupRequestEligibleStudentsHandler(IAppDbContext db, ICurrentUserService currentUser, MakeupCreditService credits)
    {
        _db = db;
        _currentUser = currentUser;
        _credits = credits;
    }

    public async Task<IReadOnlyList<MakeupEligibleLineDto>> Handle(GetMakeupRequestEligibleStudentsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        // Instructor → yalnız kendi instructorProfileId'siyle eşleşen line'lar (v1.1.2 gizlilik); manager → tümü.
        var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        return await MakeupEligibleLineQuery.BuildAsync(_db, _credits, studioId, ownProfileId, request.Level, cancellationToken);
    }
}

// ---- Staff: talep listesi (Instructor → kendi; Owner/StudioAdmin → tümü) ----
public sealed record GetMakeupRequestsQuery(MakeupRequestStatus? Status) : IQuery<IReadOnlyList<MakeupRequestDto>>;

public sealed class GetMakeupRequestsHandler : IQueryHandler<GetMakeupRequestsQuery, IReadOnlyList<MakeupRequestDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMakeupRequestsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MakeupRequestDto>> Handle(GetMakeupRequestsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var query = _db.MakeupRequests.Where(r => r.StudioId == studioId);

        // Instructor yalnız kendi (RequestedInstructorProfileId = kendi profili) taleplerini görür.
        var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (ownProfileId is { } own)
            query = query.Where(r => r.RequestedInstructorProfileId == own);

        if (request.Status is { } st)
            query = query.Where(r => r.Status == st);

        var rows = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        return await MakeupRequestView.BuildAsync(_db, studioId, rows, cancellationToken);
    }
}

// ---- Staff: tek talep ----
public sealed record GetMakeupRequestQuery(Guid Id) : IQuery<MakeupRequestDto>;

public sealed class GetMakeupRequestHandler : IQueryHandler<GetMakeupRequestQuery, MakeupRequestDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMakeupRequestHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MakeupRequestDto> Handle(GetMakeupRequestQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.MakeupRequests
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Telafi talebi", request.Id);

        // Instructor yalnız kendi talebini görebilir.
        var ownProfileId = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (ownProfileId is { } own && entity.RequestedInstructorProfileId != own)
            throw new ForbiddenException("Bu telafi talebi için yetkiniz yok.");

        var dtos = await MakeupRequestView.BuildAsync(_db, studioId, new[] { entity }, cancellationToken);
        return dtos[0];
    }
}

// ---- Student: kendi telafi talepleri (salt-okunur — programda gösterim) ----
public sealed record GetMyMakeupRequestsQuery : IQuery<IReadOnlyList<MakeupRequestDto>>;

public sealed class GetMyMakeupRequestsHandler : IQueryHandler<GetMyMakeupRequestsQuery, IReadOnlyList<MakeupRequestDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyMakeupRequestsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MakeupRequestDto>> Handle(GetMyMakeupRequestsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return System.Array.Empty<MakeupRequestDto>();

        // Öğrenci yalnız Pending + Approved görür (kendi programındaki anlamlı durumlar; Rejected/Withdrawn gizli).
        var rows = await _db.MakeupRequests
            .Where(r => r.StudioId == studioId && r.StudentProfileId == profileId
                        && (r.Status == MakeupRequestStatus.Pending || r.Status == MakeupRequestStatus.Approved))
            .OrderBy(r => r.StartAt)
            .ToListAsync(cancellationToken);

        return await MakeupRequestView.BuildAsync(_db, studioId, rows, cancellationToken);
    }
}
