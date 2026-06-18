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
/// Self-service stüdyoya katılım isteği (Sprint 15; Sprint 17'de yalnız Student'a daraltıldı). Kullanıcı
/// **JoinCode** + **rol = Student** ile başvurur → Pending üyelik (RequestedByUser) oluşur. **Profil
/// oluşturulmaz** — StudentProfile yalnızca stüdyo onayında (AcceptMembership) yaratılır. **Hoca JoinCode ile
/// başvuramaz** (Sprint 17): hocalar yalnız stüdyo davetiyle katılır → Role=Instructor 400/422. Tenant-
/// öncesidir → IStudioWriteRequest DEĞİL. Geçersiz/pasif kod generic 404 (stüdyo sızdırmaz).
/// </summary>
public sealed record ApplyToStudioCommand(string JoinCode, StudioRole Role) : ICommand<PendingInvitationDto>;

public sealed class ApplyToStudioValidator : AbstractValidator<ApplyToStudioCommand>
{
    public ApplyToStudioValidator()
    {
        RuleFor(x => x.JoinCode).NotEmpty().MaximumLength(40);
        // Sprint 17: yalnız Öğrenci kodla başvurabilir. Hoca → stüdyo daveti zorunlu.
        RuleFor(x => x.Role)
            .Must(r => r == StudioRole.Student)
            .WithMessage("Hocalar bir stüdyoya kodla başvuramaz; stüdyonun davet etmesi gerekir.");
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

        // Sprint 17 güvenliği: yalnız Öğrenci kodla başvurabilir (validator de engeller; handler ikinci kapı).
        // Hoca → stüdyo daveti zorunlu (doğrudan komut çağrısına karşı 400).
        if (request.Role != StudioRole.Student)
            throw new Pliaforma.Application.Common.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                    ["Role"] = new[] { "Hocalar bir stüdyoya kodla başvuramaz; stüdyonun davet etmesi gerekir." },
                });

        // Sprint 17 Rev2: e-posta doğrulaması artık BAŞVURU aşamasında ZORUNLU DEĞİL. Öğrenci doğrulanmadan da
        // Pending başvuru oluşturabilir. Asıl gate ÜYELİK AKTİFLEŞİRKEN (AcceptMembership) uygulanır.
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
            return new PendingInvitationDto(existing.Id, studio.Id, studio.Name, studio.Slug, existing.Role, existing.CreatedAt);
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

        return new PendingInvitationDto(membership.Id, studio.Id, studio.Name, studio.Slug, membership.Role, membership.CreatedAt);
    }
}
