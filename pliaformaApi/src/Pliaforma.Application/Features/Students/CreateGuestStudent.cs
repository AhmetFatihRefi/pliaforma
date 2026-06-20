using FluentValidation;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Misafir öğrenci oluşturur (User'sız). Owner/StudioAdmin/Instructor erişebilir. CreatedByUserId
/// mevcut kullanıcıdır; StudioId aktif stüdyo bağlamından alınır (body'den DEĞİL).
/// </summary>
public sealed record CreateGuestStudentCommand(
    string FullName,
    string? Phone,
    string? Email,
    StudentSource Source,
    string? Notes,
    string? HealthNotes,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateOnly? BirthDate,
    StudentLevel? Level = null) : ICommand<StudentDto>, IStudioWriteRequest;

public sealed class CreateGuestStudentValidator : AbstractValidator<CreateGuestStudentCommand>
{
    public CreateGuestStudentValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.HealthNotes).MaximumLength(2000);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(32);
    }
}

public sealed class CreateGuestStudentHandler : ICommandHandler<CreateGuestStudentCommand, StudentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateGuestStudentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(CreateGuestStudentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var entity = new StudentProfile
        {
            StudioId = studioId,
            UserId = null,
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Status = StudentStatus.Guest,
            Source = request.Source,
            Level = request.Level,
            Notes = request.Notes?.Trim(),
            HealthNotes = request.HealthNotes?.Trim(),
            EmergencyContactName = request.EmergencyContactName?.Trim(),
            EmergencyContactPhone = request.EmergencyContactPhone?.Trim(),
            BirthDate = request.BirthDate,
            CreatedByUserId = userId,
            IsActive = true,
        };

        _db.StudentProfiles.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // Oluşturan kişi sağlık notunu görebilir.
        return StudentDto.From(entity, includeHealth: true);
    }
}
