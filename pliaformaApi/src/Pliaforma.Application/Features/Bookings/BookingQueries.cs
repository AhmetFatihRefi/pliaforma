using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

internal static class BookingProjection
{
    public static IQueryable<ClassBookingDto> Project(IQueryable<Domain.Entities.ClassBooking> q) =>
        q.Select(b => new ClassBookingDto(
            b.Id, b.ClassSessionId, b.StudentProfileId, b.StudentProfile!.FullName, b.Status,
            b.ClassSession!.StartAt, b.ClassSession.EndAt,
            b.ClassSession.ClassType!.Name, b.ClassSession.InstructorProfile!.DisplayName, b.CancelledAt));
}

// ---- Stüdyo rezervasyon listesi (Instructor → yalnızca kendi seansları) ----
public sealed record GetStudioBookingsQuery(Guid? ClassSessionId, ClassBookingStatus? Status)
    : IQuery<IReadOnlyList<ClassBookingDto>>;

public sealed class GetStudioBookingsHandler : IQueryHandler<GetStudioBookingsQuery, IReadOnlyList<ClassBookingDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudioBookingsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ClassBookingDto>> Handle(GetStudioBookingsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var query = _db.ClassBookings.Where(b => b.StudioId == studioId);

        var own = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (own is { } ownId)
            query = query.Where(b => b.ClassSession!.InstructorProfileId == ownId);

        if (request.ClassSessionId is { } sid)
            query = query.Where(b => b.ClassSessionId == sid);
        if (request.Status is { } st)
            query = query.Where(b => b.Status == st);

        return await BookingProjection.Project(query.OrderByDescending(b => b.ClassSession!.StartAt)).ToListAsync(cancellationToken);
    }
}

// ---- Tek rezervasyon ----
public sealed record GetStudioBookingQuery(Guid Id) : IQuery<ClassBookingDto>;

public sealed class GetStudioBookingHandler : IQueryHandler<GetStudioBookingQuery, ClassBookingDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetStudioBookingHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClassBookingDto> Handle(GetStudioBookingQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var dto = await BookingProjection
            .Project(_db.ClassBookings.Where(b => b.Id == request.Id && b.StudioId == studioId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Rezervasyon", request.Id);

        var own = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (own is { } ownId)
        {
            var sessionInstructor = await _db.ClassSessions.Where(s => s.Id == dto.ClassSessionId).Select(s => s.InstructorProfileId).FirstAsync(cancellationToken);
            if (sessionInstructor != ownId)
                throw new ForbiddenException("Bu rezervasyon için yetkiniz yok.");
        }
        return dto;
    }
}

// ---- Bir seansın rezervasyonları ----
public sealed record GetSessionBookingsQuery(Guid ClassSessionId) : IQuery<IReadOnlyList<ClassBookingDto>>;

public sealed class GetSessionBookingsHandler : IQueryHandler<GetSessionBookingsQuery, IReadOnlyList<ClassBookingDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSessionBookingsHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ClassBookingDto>> Handle(GetSessionBookingsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.ClassSessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.ClassSessionId);

        var own = await SchedulingAccess.CurrentInstructorProfileIdAsync(_db, _currentUser, studioId, cancellationToken);
        if (own is { } ownId && session.InstructorProfileId != ownId)
            throw new ForbiddenException("Bu seans için yetkiniz yok.");

        return await BookingProjection
            .Project(_db.ClassBookings.Where(b => b.StudioId == studioId && b.ClassSessionId == request.ClassSessionId)
                .OrderBy(b => b.Status).ThenBy(b => b.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
