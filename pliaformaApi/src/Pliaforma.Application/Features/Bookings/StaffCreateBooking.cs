using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>
/// Personel (Owner/StudioAdmin/Instructor) bir öğrenci adına vekaleten rezervasyon yapar. Inactive
/// öğrenci için yapılamaz; Guest için yapılabilir. Instructor yalnızca KENDİ seansına ve KENDİ
/// oluşturduğu öğrenci için.
/// </summary>
public sealed record StaffCreateBookingCommand(Guid ClassSessionId, Guid StudentProfileId)
    : ICommand<ClassBookingDto>, IStudioWriteRequest;

public sealed class StaffCreateBookingValidator : AbstractValidator<StaffCreateBookingCommand>
{
    public StaffCreateBookingValidator()
    {
        RuleFor(x => x.ClassSessionId).NotEmpty();
        RuleFor(x => x.StudentProfileId).NotEmpty();
    }
}

public sealed class StaffCreateBookingHandler : ICommandHandler<StaffCreateBookingCommand, ClassBookingDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly BookingCreator _creator;

    public StaffCreateBookingHandler(IAppDbContext db, ICurrentUserService currentUser, BookingCreator creator)
    {
        _db = db;
        _currentUser = currentUser;
        _creator = creator;
    }

    public async Task<ClassBookingDto> Handle(StaffCreateBookingCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.ClassSessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.ClassSessionId);

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Öğrenci", request.StudentProfileId);

        if (student.Status == StudentStatus.Inactive)
            throw new BusinessRuleException("Pasif öğrenci için rezervasyon yapılamaz.");

        // Instructor kısıtı: kendi seansı + kendi oluşturduğu öğrenci.
        if (_currentUser.StudioRole == StudioRole.Instructor)
        {
            var ownsSession = await _db.InstructorProfiles
                .AnyAsync(p => p.Id == session.InstructorProfileId && p.UserId == userId, cancellationToken);
            if (!ownsSession)
                throw new ForbiddenException("Yalnızca kendi seanslarınıza rezervasyon yapabilirsiniz.");
            if (student.CreatedByUserId != userId)
                throw new ForbiddenException("Yalnızca kendi oluşturduğunuz öğrenciler için rezervasyon yapabilirsiniz.");
        }

        var bookerRole = _currentUser.StudioRole ?? StudioRole.Instructor;
        var booking = await _creator.CreateAsync(studioId, session, student, userId, bookerRole, cancellationToken);

        var classTypeName = await _db.ClassTypes.Where(c => c.Id == session.ClassTypeId).Select(c => c.Name).FirstAsync(cancellationToken);
        var instructorName = await _db.InstructorProfiles.Where(p => p.Id == session.InstructorProfileId).Select(p => p.DisplayName).FirstAsync(cancellationToken);

        return new ClassBookingDto(booking.Id, session.Id, student.Id, student.FullName, booking.Status,
            session.StartAt, session.EndAt, classTypeName, instructorName, null);
    }
}
