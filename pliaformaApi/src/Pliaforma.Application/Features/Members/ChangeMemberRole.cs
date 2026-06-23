using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Bir üyenin rolünü StudioAdmin veya Instructor olarak değiştirir. **Yalnızca Owner** (controller'da
/// kısıtlı). Owner'ın rolü değiştirilemez; Owner rolüne yükseltme bu uçtan yapılamaz. Instructor'a
/// geçişte InstructorProfile yoksa oluşturulur.
/// </summary>
public sealed record ChangeMemberRoleCommand(Guid MembershipId, StudioRole Role) : ICommand<Unit>, IStudioWriteRequest;

public sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.Role)
            .Must(r => r is StudioRole.StudioAdmin or StudioRole.Instructor)
            .WithMessage("Rol yalnızca StudioAdmin veya Instructor olabilir.");
    }
}

public sealed class ChangeMemberRoleHandler : ICommandHandler<ChangeMemberRoleCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecurityAuditLogger _audit;

    public ChangeMemberRoleHandler(IAppDbContext db, ICurrentUserService currentUser, ISecurityAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Unit> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId && m.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Üyelik", request.MembershipId);

        if (membership.Role == StudioRole.Owner)
            throw new BusinessRuleException("Stüdyo sahibinin rolü değiştirilemez.");

        if (membership.Role == request.Role)
            return Unit.Value;

        var oldRole = membership.Role;
        membership.Role = request.Role;

        // Instructor'a geçişte profil yoksa oluştur (ücret 0; Owner sonra günceller).
        if (request.Role == StudioRole.Instructor)
        {
            var hasProfile = await _db.InstructorProfiles
                .AnyAsync(p => p.StudioId == studioId && p.UserId == membership.UserId, cancellationToken);
            if (!hasProfile)
            {
                var userName = await _db.Users.Where(u => u.Id == membership.UserId)
                    .Select(u => u.FullName).FirstAsync(cancellationToken);
                _db.InstructorProfiles.Add(new InstructorProfile
                {
                    StudioId = studioId,
                    UserId = membership.UserId,
                    DisplayName = userName,
                    Rate = 0m,
                    RateType = RateType.PerSession,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Denetim (31C-1): rol değişimi — eski/yeni rol; PII yok.
        await _audit.LogAsync(SecurityAuditEventType.RoleChanged, SecurityAuditOutcome.Success,
            actorUserId: _currentUser.UserId, studioId: studioId,
            targetType: "StudioMembership", targetId: membership.Id,
            detail: $"role: {oldRole}->{request.Role}", cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
