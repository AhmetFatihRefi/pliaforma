using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Aktif stüdyoya kayıtlı bir kullanıcıyı StudioAdmin veya Instructor olarak davet eder. Pending
/// üyelik (InvitedByStudio) oluşturur; Instructor ise ek olarak InstructorProfile (ücretle).
/// **Yalnızca Owner, StudioAdmin rolü ile davet edebilir** (StudioAdmin yalnızca Instructor davet eder).
/// </summary>
public sealed record InviteMemberCommand(
    string Email,
    StudioRole Role,
    string? DisplayName,
    decimal? Rate,
    RateType? RateType) : ICommand<MemberDto>, IStudioWriteRequest;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role)
            .Must(r => r is StudioRole.StudioAdmin or StudioRole.Instructor)
            .WithMessage("Rol yalnızca StudioAdmin veya Instructor olabilir.");
        RuleFor(x => x.DisplayName).MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0).When(x => x.Rate.HasValue);
    }
}

public sealed class InviteMemberHandler : ICommandHandler<InviteMemberCommand, MemberDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InviteMemberHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MemberDto> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        // Owner koruması: yalnızca Owner, StudioAdmin atayabilir.
        if (request.Role == StudioRole.StudioAdmin && _currentUser.StudioRole != StudioRole.Owner)
            throw new ForbiddenException("Yalnızca stüdyo sahibi StudioAdmin davet edebilir.");

        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken)
            ?? throw new ConflictException("Bu e-posta ile kayıtlı kullanıcı yok; önce hesap oluşturmalı.");

        var existing = await _db.StudioMemberships
            .AnyAsync(m => m.StudioId == studioId && m.UserId == user.Id, cancellationToken);
        if (existing)
            throw new ConflictException("Bu kullanıcı zaten bu stüdyonun üyesi (veya bekleyen daveti var).");

        var membership = new StudioMembership
        {
            StudioId = studioId,
            UserId = user.Id,
            Role = request.Role,
            Status = MembershipStatus.Pending,
            Direction = MembershipDirection.InvitedByStudio,
        };
        _db.StudioMemberships.Add(membership);

        InstructorProfile? profile = null;
        if (request.Role == StudioRole.Instructor)
        {
            profile = new InstructorProfile
            {
                StudioId = studioId,
                UserId = user.Id,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.FullName : request.DisplayName.Trim(),
                Rate = request.Rate ?? 0m,
                RateType = request.RateType ?? Domain.Enums.RateType.PerSession,
            };
            _db.InstructorProfiles.Add(profile);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new MemberDto(membership.Id, user.Id, user.FullName, user.Email, membership.Role,
            membership.Status, membership.Direction, profile?.Id, profile?.Rate, profile?.RateType);
    }
}
