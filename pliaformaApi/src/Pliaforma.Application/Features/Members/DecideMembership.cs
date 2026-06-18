using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Bekleyen bir üyeliği kabul eder. Yetki yön bazlıdır (MembershipAccess). Tenant-öncesi olabilir
/// (davet edilen kullanıcının o stüdyoda henüz aktif üyeliği yoktur) → IStudioWriteRequest DEĞİL.
/// </summary>
public sealed record AcceptMembershipCommand(Guid MembershipId) : ICommand<Unit>;

public sealed class AcceptMembershipHandler : ICommandHandler<AcceptMembershipCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public AcceptMembershipHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Unit> Handle(AcceptMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Status != MembershipStatus.Pending)
            throw new BusinessRuleException("Yalnızca bekleyen üyelikler kabul edilebilir.");

        await MembershipAccess.EnsureCanDecideAsync(_db, _currentUser, membership, cancellationToken);

        // Sprint 17: davet kabulünde (InvitedByStudio) davet edilen kullanıcının e-postası doğrulanmış olmalı.
        // (RequestedByUser başvurusu zaten apply sırasında e-posta doğrulaması gerektirir — Sprint 16.)
        // Reddetmede bu şart yoktur. 403 → kullanıcı önce doğrulasın, sonra kabul etsin.
        if (membership.Direction == MembershipDirection.InvitedByStudio)
        {
            var verified = await _db.Users
                .Where(u => u.Id == membership.UserId)
                .Select(u => u.EmailVerifiedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (verified is null)
                throw new ForbiddenException("Daveti kabul etmek için önce e-posta adresinizi doğrulayın.");
        }

        membership.Status = MembershipStatus.Active;
        membership.AcceptedAt = _clock.UtcNow;

        // Profil onayda oluşur (idempotent): self-service başvuruda profil yaratılmamıştı; invite yolu
        // Instructor profili önceden oluşturduysa burada no-op olur (çift profil yok).
        await EnsureRoleProfileAsync(membership, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    /// <summary>Üyelik rolüne göre eksik profili (Student/Instructor) onay sırasında oluşturur. Varsa dokunmaz.</summary>
    private async Task EnsureRoleProfileAsync(StudioMembership membership, CancellationToken ct)
    {
        var deciderUserId = _currentUser.RequireUserId();

        if (membership.Role == StudioRole.Instructor)
        {
            var hasProfile = await _db.InstructorProfiles
                .AnyAsync(p => p.StudioId == membership.StudioId && p.UserId == membership.UserId, ct);
            if (hasProfile) return;

            var user = await _db.Users.FirstAsync(u => u.Id == membership.UserId, ct);
            _db.InstructorProfiles.Add(new InstructorProfile
            {
                StudioId = membership.StudioId,
                UserId = membership.UserId,
                DisplayName = user.FullName,
                Rate = 0m,                 // stüdyo onayladıktan sonra ücreti belirler
                RateType = RateType.PerSession,
            });
        }
        else if (membership.Role == StudioRole.Student)
        {
            var hasProfile = await _db.StudentProfiles
                .AnyAsync(p => p.StudioId == membership.StudioId && p.UserId == membership.UserId, ct);
            if (hasProfile) return;

            var user = await _db.Users.FirstAsync(u => u.Id == membership.UserId, ct);
            _db.StudentProfiles.Add(new StudentProfile
            {
                StudioId = membership.StudioId,
                UserId = membership.UserId,
                StudioMembershipId = membership.Id,
                FullName = user.FullName,
                Phone = user.Phone,
                Email = user.Email,
                Status = StudentStatus.Active,
                Source = StudentSource.Web,
                CreatedByUserId = deciderUserId,
                ConvertedAt = _clock.UtcNow,
            });
        }
    }
}

/// <summary>Bekleyen bir üyeliği reddeder (yetki yön bazlı).</summary>
public sealed record RejectMembershipCommand(Guid MembershipId) : ICommand<Unit>;

public sealed class RejectMembershipHandler : ICommandHandler<RejectMembershipCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RejectMembershipHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RejectMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Status != MembershipStatus.Pending)
            throw new BusinessRuleException("Yalnızca bekleyen üyelikler reddedilebilir.");

        await MembershipAccess.EnsureCanDecideAsync(_db, _currentUser, membership, cancellationToken);

        membership.Status = MembershipStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
