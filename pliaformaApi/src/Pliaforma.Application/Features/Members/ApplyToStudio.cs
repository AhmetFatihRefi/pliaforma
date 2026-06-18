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
/// Self-service stüdyoya katılım isteği (Sprint 15). Kullanıcı **JoinCode** + **rol** (yalnız Student/
/// Instructor) ile başvurur → Pending üyelik (RequestedByUser) oluşur. **Profil oluşturulmaz** — Student/
/// InstructorProfile yalnızca stüdyo onayında (AcceptMembership) yaratılır. Tenant-öncesidir (kullanıcının o
/// stüdyoda üyeliği yoktur) → IStudioWriteRequest DEĞİL. Geçersiz/pasif kod generic 404 (stüdyo sızdırmaz).
/// </summary>
public sealed record ApplyToStudioCommand(string JoinCode, StudioRole Role) : ICommand<PendingInvitationDto>;

public sealed class ApplyToStudioValidator : AbstractValidator<ApplyToStudioCommand>
{
    public ApplyToStudioValidator()
    {
        RuleFor(x => x.JoinCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Role)
            .Must(r => r is StudioRole.Student or StudioRole.Instructor)
            .WithMessage("Yalnızca Öğrenci veya Hoca olarak başvurabilirsiniz.");
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

        // Güvenlik: yalnız Student/Instructor (validator de engeller; handler ikinci kapı).
        if (request.Role is not (StudioRole.Student or StudioRole.Instructor))
            throw new BusinessRuleException("Yalnızca Öğrenci veya Hoca olarak başvurabilirsiniz.");

        // Sprint 16: e-posta doğrulanmadan stüdyoya katılım isteği gönderilemez (login/logout/resend açık).
        var verified = await _db.Users.Where(u => u.Id == userId).Select(u => u.EmailVerifiedAt).FirstOrDefaultAsync(cancellationToken);
        if (verified is null)
            throw new ForbiddenException("Stüdyoya katılmak için önce e-posta adresinizi doğrulayın.");

        var code = JoinCode.Normalize(request.JoinCode);
        // Geçersiz/pasif kod → generic 404 (stüdyo varlığı/ismi sızdırılmaz).
        var studio = await _db.Studios.FirstOrDefaultAsync(
            s => s.JoinCode == code && s.JoinCodeIsActive, cancellationToken);
        if (studio is null || string.IsNullOrEmpty(code))
            throw new NotFoundException("Geçersiz veya pasif katılım kodu.");

        var existing = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.StudioId == studio.Id && m.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            // Active/Pending → tekrar başvurulamaz. Rejected/Removed → mevcut satır Pending'e döner.
            if (existing.Status is MembershipStatus.Active or MembershipStatus.Pending)
                throw new ConflictException("Bu stüdyoya zaten üyesiniz veya bekleyen başvurunuz var.");

            existing.Role = request.Role;
            existing.Status = MembershipStatus.Pending;
            existing.Direction = MembershipDirection.RequestedByUser;
            existing.AcceptedAt = null;
            await _db.SaveChangesAsync(cancellationToken);
            return new PendingInvitationDto(existing.Id, studio.Id, studio.Name, studio.Slug, existing.Role);
        }

        var membership = new StudioMembership
        {
            StudioId = studio.Id,
            UserId = userId,
            Role = request.Role,
            Status = MembershipStatus.Pending,
            Direction = MembershipDirection.RequestedByUser,
        };
        _db.StudioMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return new PendingInvitationDto(membership.Id, studio.Id, studio.Name, studio.Slug, membership.Role);
    }
}
