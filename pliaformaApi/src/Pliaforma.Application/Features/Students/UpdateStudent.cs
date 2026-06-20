using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Öğrenci bilgilerini günceller. Tenant + yetki (Owner/StudioAdmin tümü, Instructor yalnızca kendi
/// oluşturduğu) kontrol edilir. Status/UserId burada DEĞİŞTİRİLMEZ (ayrı uçlar).
/// </summary>
public sealed record UpdateStudentCommand(
    Guid Id,
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

public sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.HealthNotes).MaximumLength(2000);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(32);
    }
}

public sealed class UpdateStudentHandler : ICommandHandler<UpdateStudentCommand, StudentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateStudentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StudentDto> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.Id);

        StudentAccess.EnsureCanManage(_currentUser, entity);

        entity.FullName = request.FullName.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.Email = request.Email?.Trim();
        entity.Source = request.Source;
        entity.Level = request.Level;
        entity.Notes = request.Notes?.Trim();
        entity.HealthNotes = request.HealthNotes?.Trim();
        entity.EmergencyContactName = request.EmergencyContactName?.Trim();
        entity.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        entity.BirthDate = request.BirthDate;

        await _db.SaveChangesAsync(cancellationToken);
        return StudentDto.From(entity, includeHealth: StudentAccess.CanSeeHealth(_currentUser, entity));
    }
}
