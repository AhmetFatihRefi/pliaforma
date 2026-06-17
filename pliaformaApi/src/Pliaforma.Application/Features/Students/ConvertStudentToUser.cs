using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Text;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Misafir öğrenciyi gerçek bir User'a bağlar. **StudentProfile.Id KORUNUR** (silinmez) — yalnızca
/// UserId/StudioMembershipId set edilir, Status=Active olur. İki senaryo: e-posta mevcut User'a
/// aitse o kullanılır (duplicate yaratılmaz); değilse yeni User + Student üyeliği oluşturulur.
/// </summary>
public sealed record ConvertStudentToUserCommand(
    Guid Id,
    string Email,
    string? InitialPassword) : ICommand<StudentDto>, IStudioWriteRequest;

public sealed class ConvertStudentToUserValidator : AbstractValidator<ConvertStudentToUserCommand>
{
    public ConvertStudentToUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.InitialPassword)
            .MinimumLength(8).MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.InitialPassword));
    }
}

public sealed class ConvertStudentToUserHandler : ICommandHandler<ConvertStudentToUserCommand, StudentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTime _clock;

    public ConvertStudentToUserHandler(IAppDbContext db, ICurrentUserService currentUser, IPasswordHasher hasher, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<StudentDto> Handle(ConvertStudentToUserCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.Id);

        StudentAccess.EnsureCanManage(_currentUser, student);

        if (student.UserId is not null)
            throw new ConflictException("Bu öğrenci zaten bir kullanıcıya bağlı.");
        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenci dönüştürülemez.");

        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.InitialPassword))
                throw new BusinessRuleException("Yeni kullanıcı oluşturmak için başlangıç parolası gerekli.");

            user = new User
            {
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _hasher.Hash(request.InitialPassword),
                FullName = student.FullName,
                Phone = student.Phone,
                GlobalRole = GlobalRole.Standard,
            };
            _db.Users.Add(user);
        }
        else
        {
            // Aynı stüdyoda bu kullanıcıya bağlı BAŞKA aktif öğrenci profili varsa çakışma.
            var dup = await _db.StudentProfiles.AnyAsync(
                s => s.StudioId == studioId && s.UserId == user.Id && s.Id != student.Id && s.IsActive,
                cancellationToken);
            if (dup)
                throw new ConflictException("Bu kullanıcıya bağlı aktif bir öğrenci kaydı zaten var.");
        }

        // Stüdyo üyeliği: varsa onu kullan (StudioMembership (StudioId,UserId) benzersizdir),
        // yoksa Student üyeliği oluştur.
        var membership = await _db.StudioMemberships
            .FirstOrDefaultAsync(m => m.StudioId == studioId && m.UserId == user.Id, cancellationToken);
        if (membership is null)
        {
            membership = new StudioMembership
            {
                StudioId = studioId,
                UserId = user.Id,
                Role = StudioRole.Student,
                Status = MembershipStatus.Active,
                Direction = MembershipDirection.InvitedByStudio,
                AcceptedAt = _clock.UtcNow,
            };
            _db.StudioMemberships.Add(membership);
        }

        // Profil KORUNUR — yalnızca bağlanır.
        student.UserId = user.Id;
        student.StudioMembershipId = membership.Id;
        student.Email = request.Email.Trim();
        student.Status = StudentStatus.Active;
        student.IsActive = true;
        student.ConvertedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return StudentDto.From(student, includeHealth: StudentAccess.CanSeeHealth(_currentUser, student));
    }
}
