using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>Yoklama zaman/durum kuralları (gelecek seans + iptal seans engeli).</summary>
internal static class AttendanceRules
{
    public static async Task EnsureSessionMarkableAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, ClassSession session, CancellationToken ct)
    {
        if (session.Status == ClassSessionStatus.Cancelled)
            throw new ConflictException("İptal edilmiş seans için yoklama alınamaz.");

        // Yalnızca başlamış/bitmiş seans (StartAt geçmiş). Gelecek seans → 409.
        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        if (session.StartAt > nowLocal)
            throw new ConflictException("Gelecek seans için yoklama alınamaz.");
    }
}

// ---- Batch yoklama ----
public sealed record MarkAttendanceCommand(Guid SessionId, IReadOnlyList<AttendanceMarkInput> Items)
    : ICommand<SessionAttendanceDto>, IStudioWriteRequest;

public sealed class MarkAttendanceValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.BookingId).NotEmpty();
            i.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }
}

public sealed class MarkAttendanceHandler : ICommandHandler<MarkAttendanceCommand, SessionAttendanceDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly AttendanceMarker _marker;

    public MarkAttendanceHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, AttendanceMarker marker)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _marker = marker;
    }

    public async Task<SessionAttendanceDto> Handle(MarkAttendanceCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.SessionId);

        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);
        await AttendanceRules.EnsureSessionMarkableAsync(_db, _clock, studioId, session, cancellationToken);

        await _marker.MarkAsync(studioId, session, userId, _currentUser.StudioRole, request.Items, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await AttendanceViews.BuildSessionViewAsync(_db, _clock, studioId, session, cancellationToken);
    }
}

// ---- Tek booking yoklaması ----
public sealed record MarkBookingAttendanceCommand(
    Guid BookingId, AttendanceStatus Status, string? Notes = null, bool GrantMakeupCredit = false)
    : ICommand<Unit>, IStudioWriteRequest;

public sealed class MarkBookingAttendanceValidator : AbstractValidator<MarkBookingAttendanceCommand>
{
    public MarkBookingAttendanceValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class MarkBookingAttendanceHandler : ICommandHandler<MarkBookingAttendanceCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly AttendanceMarker _marker;

    public MarkBookingAttendanceHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, AttendanceMarker marker)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _marker = marker;
    }

    public async Task<Unit> Handle(MarkBookingAttendanceCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var booking = await _db.ClassBookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId && b.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Rezervasyon", request.BookingId);

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == booking.ClassSessionId && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", booking.ClassSessionId);

        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);
        await AttendanceRules.EnsureSessionMarkableAsync(_db, _clock, studioId, session, cancellationToken);

        var items = new[]
        {
            new AttendanceMarkInput(booking.Id, request.Status, request.Notes, request.GrantMakeupCredit),
        };
        await _marker.MarkAsync(studioId, session, userId, _currentUser.StudioRole, items, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
