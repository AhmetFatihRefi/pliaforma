using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Settings;
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
    private readonly AuthSettings _auth;
    private readonly ISecurityAuditLogger _audit;

    public AcceptMembershipHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, IOptions<AuthSettings> auth, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _auth = auth.Value;
        _audit = audit;
    }

    public async Task<Unit> Handle(AcceptMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Status != MembershipStatus.Pending)
            throw new BusinessRuleException("Yalnızca bekleyen üyelikler kabul edilebilir.");

        await MembershipAccess.EnsureCanDecideAsync(_db, _currentUser, membership, cancellationToken);

        // Sprint 17 Rev2: üyelik AKTİFLEŞİRKEN, üye olacak kullanıcının e-postası doğrulanmış olmalı.
        // HER İKİ YÖN için geçerli: InvitedByStudio (hoca daveti kabulü) + RequestedByUser (öğrenci başvuru
        // onayı). Doğrulanmamışsa active yapılmaz. Reddetmede bu şart yoktur.
        // Auth.RequireEmailVerification=false (dev) → bu kapı uygulanmaz (prod'da açılır).
        if (_auth.RequireEmailVerification)
        {
            var verified = await _db.Users
                .Where(u => u.Id == membership.UserId)
                .Select(u => u.EmailVerifiedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (verified is null)
                throw new ForbiddenException("Üyeliğin aktifleşmesi için önce e-posta adresi doğrulanmalıdır.");
        }

        membership.Status = MembershipStatus.Active;
        membership.AcceptedAt = _clock.UtcNow;

        // Profil onayda oluşur (idempotent): self-service başvuruda profil yaratılmamıştı; invite yolu
        // Instructor profili önceden oluşturduysa burada no-op olur (çift profil yok).
        await EnsureRoleProfileAsync(membership, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Denetim (31C-1): üyelik kabul edildi.
        await _audit.LogAsync(SecurityAuditEventType.MembershipAccepted, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: membership.StudioId,
            targetType: "StudioMembership", targetId: membership.Id,
            detail: $"role: {membership.Role}", cancellationToken: cancellationToken);

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
    private readonly ISecurityAuditLogger _audit;

    public RejectMembershipHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
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

        // Denetim (31C-1): üyelik reddedildi.
        await _audit.LogAsync(SecurityAuditEventType.MembershipRejected, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: membership.StudioId,
            targetType: "StudioMembership", targetId: membership.Id,
            detail: $"role: {membership.Role}", cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
