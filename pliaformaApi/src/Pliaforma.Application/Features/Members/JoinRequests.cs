using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Members;

/// <summary>Stüdyo tarafı bekleyen katılım isteği (Owner/StudioAdmin görür).</summary>
public sealed record JoinRequestDto(
    Guid MembershipId,
    string ApplicantName,
    string ApplicantEmail,
    StudioRole RequestedRole,
    DateTime RequestedAt,
    MembershipDirection Direction,
    MembershipStatus Status);

/// <summary>Kullanıcının kendi katılım başvurusu (bekleyen/reddedilen).</summary>
public sealed record MyJoinRequestDto(
    Guid MembershipId,
    Guid StudioId,
    string StudioName,
    StudioRole RequestedRole,
    MembershipStatus Status,
    DateTime RequestedAt,
    DateTime? RejectedAt,
    string? Message);

// ---- Stüdyo: bekleyen katılım istekleri ----
public sealed record GetJoinRequestsQuery : IQuery<IReadOnlyList<JoinRequestDto>>;

public sealed class GetJoinRequestsHandler : IQueryHandler<GetJoinRequestsQuery, IReadOnlyList<JoinRequestDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetJoinRequestsHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<JoinRequestDto>> Handle(GetJoinRequestsQuery request, CancellationToken ct)
    {
        var studioId = _currentUser.RequireStudioId();
        return await _db.StudioMemberships
            .Where(m => m.StudioId == studioId
                        && m.Direction == MembershipDirection.RequestedByUser
                        && m.Status == MembershipStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new JoinRequestDto(
                m.Id, m.User!.FullName, m.User.Email, m.Role, m.CreatedAt, m.Direction, m.Status))
            .ToListAsync(ct);
    }
}

// ---- Kullanıcı: kendi katılım başvurularım ----
public sealed record GetMyJoinRequestsQuery : IQuery<IReadOnlyList<MyJoinRequestDto>>;

public sealed class GetMyJoinRequestsHandler : IQueryHandler<GetMyJoinRequestsQuery, IReadOnlyList<MyJoinRequestDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetMyJoinRequestsHandler(IAppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<IReadOnlyList<MyJoinRequestDto>> Handle(GetMyJoinRequestsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        // Yalnız kendi başvuruları (RequestedByUser); bekleyen + reddedilen. Başka kullanıcı/stüdyo sızmaz.
        return await _db.StudioMemberships
            .Where(m => m.UserId == userId
                        && m.Direction == MembershipDirection.RequestedByUser
                        && (m.Status == MembershipStatus.Pending || m.Status == MembershipStatus.Rejected))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MyJoinRequestDto(
                m.Id, m.StudioId, m.Studio!.Name, m.Role, m.Status, m.CreatedAt,
                m.Status == MembershipStatus.Rejected ? m.UpdatedAt : null,
                null))
            .ToListAsync(ct);
    }
}
