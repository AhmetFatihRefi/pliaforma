using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

// ---- Öğrencinin kendi yaklaşan rezervasyonları ----
public sealed record GetMyBookingsQuery : IQuery<IReadOnlyList<MyBookingDto>>;

public sealed class GetMyBookingsHandler : IQueryHandler<GetMyBookingsQuery, IReadOnlyList<MyBookingDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetMyBookingsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MyBookingDto>> Handle(GetMyBookingsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
            return [];

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        return await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.StudentProfileId == profileId
                        && b.Status == ClassBookingStatus.Booked && b.ClassSession!.StartAt >= nowLocal)
            .OrderBy(b => b.ClassSession!.StartAt)
            .Select(b => new MyBookingDto(
                b.Id, b.ClassSessionId, b.ClassSession!.ClassType!.Name, b.ClassSession.InstructorProfile!.DisplayName,
                b.ClassSession.StartAt, b.ClassSession.EndAt, b.Status))
            .ToListAsync(cancellationToken);
    }
}

// ---- Öğrencinin rezerve edebileceği yaklaşan seanslar ----
public sealed record GetStudentSessionsQuery(DateOnly? From, DateOnly? To, Guid? ClassTypeId)
    : IQuery<IReadOnlyList<StudentSessionDto>>;

public sealed class GetStudentSessionsHandler : IQueryHandler<GetStudentSessionsQuery, IReadOnlyList<StudentSessionDto>>
{
    private const int MaxRangeDays = 90;
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetStudentSessionsHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<StudentSessionDto>> Handle(GetStudentSessionsQuery request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var profileId = await _db.StudentProfiles
            .Where(s => s.StudioId == studioId && s.UserId == userId && s.Status == StudentStatus.Active)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Bu stüdyoda aktif bir öğrenci profiliniz yok.");

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, cancellationToken);

        var from = request.From?.ToDateTime(TimeOnly.MinValue) ?? nowLocal;
        var to = (request.To ?? DateOnly.FromDateTime(nowLocal).AddDays(30)).AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (to.Date.Subtract(from.Date).TotalDays > MaxRangeDays)
            throw new BusinessRuleException($"Tarih aralığı en fazla {MaxRangeDays} gün olabilir.");

        var query = _db.ClassSessions.Where(s => s.StudioId == studioId
            && s.Status == ClassSessionStatus.Scheduled && s.StartAt >= from && s.StartAt < to && s.StartAt > nowLocal);
        if (request.ClassTypeId is { } ct)
            query = query.Where(s => s.ClassTypeId == ct);

        return await query
            .OrderBy(s => s.StartAt)
            .Select(s => new StudentSessionDto(
                s.Id, s.ClassType!.Name, s.InstructorProfile!.DisplayName, s.StartAt, s.EndAt, s.Capacity, s.BookedCount,
                s.SessionKind,
                _db.ClassBookings.Where(b => b.ClassSessionId == s.Id && b.StudentProfileId == profileId && b.Status == ClassBookingStatus.Booked)
                    .Select(b => (Guid?)b.Id).FirstOrDefault(),
                _db.ClassBookings.Any(b => b.ClassSessionId == s.Id && b.StudentProfileId == profileId && b.Status == ClassBookingStatus.Booked)))
            .ToListAsync(cancellationToken);
    }
}
