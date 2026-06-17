using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>
/// Öğrenci kendi adına rezervasyon yapar. Yalnızca aktif (Status=Active) kendi StudentProfile'ı ile
/// (misafir self-booking yapamaz). StudioId token'dan; öğrenci profili UserId eşleşmesiyle bulunur.
/// </summary>
public sealed record StudentBookCommand(Guid ClassSessionId) : ICommand<ClassBookingDto>, IStudioWriteRequest;

public sealed class StudentBookHandler : ICommandHandler<StudentBookCommand, ClassBookingDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly BookingCreator _creator;

    public StudentBookHandler(IAppDbContext db, ICurrentUserService currentUser, BookingCreator creator)
    {
        _db = db;
        _currentUser = currentUser;
        _creator = creator;
    }

    public async Task<ClassBookingDto> Handle(StudentBookCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.StudioId == studioId && s.UserId == userId && s.Status == StudentStatus.Active, cancellationToken)
            ?? throw new ForbiddenException("Bu stüdyoda aktif bir öğrenci profiliniz yok.");

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.ClassSessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.ClassSessionId);

        var booking = await _creator.CreateAsync(studioId, session, student, userId, StudioRole.Student, cancellationToken);

        var classTypeName = await _db.ClassTypes.Where(c => c.Id == session.ClassTypeId).Select(c => c.Name).FirstAsync(cancellationToken);
        var instructorName = await _db.InstructorProfiles.Where(p => p.Id == session.InstructorProfileId).Select(p => p.DisplayName).FirstAsync(cancellationToken);

        return new ClassBookingDto(booking.Id, session.Id, student.Id, student.FullName, booking.Status,
            session.StartAt, session.EndAt, classTypeName, instructorName, null,
            booking.RequiresPayment, booking.PackageEligibilityStatus, booking.ExpectedStudentPackageId);
    }
}
