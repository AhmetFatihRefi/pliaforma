using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Members;

/// <summary>
/// Kullanıcının bir stüdyoya (slug ile) hoca olarak başvurusu. Pending Instructor üyeliği
/// (RequestedByUser) + InstructorProfile (ücret 0; stüdyo onaylarken/ardından belirler) oluşturur.
/// Bu komut tenant-öncesidir (kullanıcının o stüdyoda üyeliği yoktur) — IStudioWriteRequest DEĞİL.
/// </summary>
public sealed record ApplyToStudioCommand(string StudioSlug) : ICommand<PendingInvitationDto>;

public sealed class ApplyToStudioValidator : AbstractValidator<ApplyToStudioCommand>
{
    public ApplyToStudioValidator()
    {
        RuleFor(x => x.StudioSlug).NotEmpty().MaximumLength(180);
    }
}

public sealed class ApplyToStudioHandler : ICommandHandler<ApplyToStudioCommand, PendingInvitationDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ApplyToStudioHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PendingInvitationDto> Handle(ApplyToStudioCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("Oturum geçersiz.");

        var studio = await _db.Studios.FirstOrDefaultAsync(s => s.Slug == request.StudioSlug.Trim(), cancellationToken)
            ?? throw new NotFoundException("Stüdyo bulunamadı.");

        var existing = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.StudioId == studio.Id && m.UserId == userId, cancellationToken);
        if (existing is not null)
            throw new ConflictException("Bu stüdyoya zaten üyesiniz veya bekleyen bir başvurunuz var.");

        var membership = new StudioMembership
        {
            StudioId = studio.Id,
            UserId = userId,
            Role = StudioRole.Instructor,
            Status = MembershipStatus.Pending,
            Direction = MembershipDirection.RequestedByUser,
        };

        var profile = new InstructorProfile
        {
            StudioId = studio.Id,
            UserId = userId,
            DisplayName = user.FullName,
            Rate = 0m,
            RateType = RateType.PerSession,
        };

        _db.StudioMemberships.Add(membership);
        _db.InstructorProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);

        return new PendingInvitationDto(membership.Id, studio.Id, studio.Name, studio.Slug, membership.Role);
    }
}
