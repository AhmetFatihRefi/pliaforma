using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

// ---- Güncelle (zaman/kapasite/not) ----
public sealed record UpdateClassSessionCommand(
    Guid Id,
    DateTime? StartAt,
    int? Capacity,
    string? Notes) : ICommand<ClassSessionDto>, IStudioWriteRequest;

public sealed class UpdateClassSessionValidator : AbstractValidator<UpdateClassSessionCommand>
{
    public UpdateClassSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1).When(x => x.Capacity.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateClassSessionHandler : ICommandHandler<UpdateClassSessionCommand, ClassSessionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public UpdateClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ClassSessionDto> Handle(UpdateClassSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.Id);

        var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);
        var classType = await _db.ClassTypes.FirstAsync(c => c.Id == session.ClassTypeId, cancellationToken);

        if (session.Status != ClassSessionStatus.Scheduled)
            throw new BusinessRuleException("Yalnızca planlı seanslar güncellenebilir.");

        if (request.StartAt is { } newStartRaw)
        {
            var newStart = DateTime.SpecifyKind(newStartRaw, DateTimeKind.Unspecified);
            var newEnd = newStart.AddMinutes(classType.DefaultDurationMinutes);
            await SessionRules.EnsureValidAsync(_db, _clock, studioId, session.InstructorProfileId, newStart, newEnd, excludeId: session.Id, cancellationToken);
            session.StartAt = newStart;
            session.EndAt = newEnd;
        }

        if (request.Capacity is { } cap)
            session.Capacity = cap;

        session.Notes = request.Notes?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return ClassSessionDto.From(session, profile.DisplayName, classType.Name);
    }
}

// ---- İptal (gerekçe + opsiyonel telafi kredisi) ----
public sealed record CancelClassSessionCommand(Guid Id, string? Reason = null, bool GrantMakeupCredit = false)
    : ICommand<Unit>, IStudioWriteRequest;

public sealed class CancelClassSessionHandler : ICommandHandler<CancelClassSessionCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly SessionCancellationService _cancellation;

    public CancelClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser, SessionCancellationService cancellation)
    {
        _db = db;
        _currentUser = currentUser;
        _cancellation = cancellation;
    }

    public async Task<Unit> Handle(CancelClassSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.Id);

        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);

        if (session.Status == ClassSessionStatus.Cancelled)
            throw new ConflictException("Bu seans zaten iptal edilmiş.");

        session.Status = ClassSessionStatus.Cancelled;

        // Cascade: aktif rezervasyonlar iptal, tüketilmiş kredi iadesi, opsiyonel telafi kredisi.
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Ders seansı iptal edildi." : request.Reason.Trim();
        await _cancellation.CancelActiveBookingsAsync(
            studioId, session.Id, userId, reason, request.GrantMakeupCredit,
            MakeupCreditReason.StudioCancelledClass, cancellationToken);
        session.BookedCount = 0;

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ---- Erteleme (reschedule) ----
public sealed record RescheduleClassSessionCommand(
    Guid Id,
    DateTime NewStartAt,
    string? Reason,
    bool KeepBookings,
    bool GrantMakeupCredit = false) : ICommand<ClassSessionDto>, IStudioWriteRequest;

public sealed class RescheduleClassSessionValidator : AbstractValidator<RescheduleClassSessionCommand>
{
    public RescheduleClassSessionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewStartAt).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RescheduleClassSessionHandler : ICommandHandler<RescheduleClassSessionCommand, ClassSessionDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly SessionCancellationService _cancellation;

    public RescheduleClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock, SessionCancellationService cancellation)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _cancellation = cancellation;
    }

    public async Task<ClassSessionDto> Handle(RescheduleClassSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.Id);

        var profile = await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);
        var classType = await _db.ClassTypes.FirstAsync(c => c.Id == session.ClassTypeId, cancellationToken);

        if (session.Status != ClassSessionStatus.Scheduled)
            throw new ConflictException("Yalnızca planlı (Scheduled) seanslar ertelenebilir.");

        var newStart = DateTime.SpecifyKind(request.NewStartAt, DateTimeKind.Unspecified);
        var newEnd = newStart.AddMinutes(classType.DefaultDurationMinutes);

        // Sprint 21: HARD = geçmiş + HOCA çakışması + ÖĞRENCİ çakışması (kendini hariç). Müsaitlik ADVISORY (engellemez).
        // KeepBookings=true ise dersin booked öğrencileri yeni saatte başka derse giremez; false ise bookings iptal → öğrenci aranmaz.
        var movingStudentIds = request.KeepBookings
            ? await _db.ClassBookings
                .Where(b => b.StudioId == studioId && b.ClassSessionId == session.Id && b.Status == ClassBookingStatus.Booked)
                .Select(b => b.StudentProfileId).ToListAsync(cancellationToken)
            : new List<Guid>();
        await SessionRules.EnsureMovableAsync(_db, _clock, studioId, session.InstructorProfileId, movingStudentIds, newStart, newEnd, new[] { session.Id }, cancellationToken);

        if (!request.KeepBookings)
        {
            // Mevcut rezervasyonlar iptal edilir; istenirse öğrencilere telafi kredisi verilir.
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Ders ertelendi." : request.Reason.Trim();
            await _cancellation.CancelActiveBookingsAsync(
                studioId, session.Id, userId, reason, request.GrantMakeupCredit,
                MakeupCreditReason.StudioCancelledClass, cancellationToken);
            session.BookedCount = 0;
        }
        // KeepBookings=true → rezervasyonlar aynı seans üzerinde kalır (seans taşınır).

        session.StartAt = newStart;
        session.EndAt = newEnd;
        session.RescheduledAt = _clock.UtcNow;
        session.RescheduleReason = request.Reason?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return ClassSessionDto.From(session, profile.DisplayName, classType.Name);
    }
}

// ---- Sil (soft delete) ----
public sealed record DeleteClassSessionCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class DeleteClassSessionHandler : ICommandHandler<DeleteClassSessionCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteClassSessionHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteClassSessionCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders seansı", request.Id);

        await SchedulingAccess.RequireManageableInstructorAsync(_db, _currentUser, studioId, session.InstructorProfileId, cancellationToken);

        _db.ClassSessions.Remove(session);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
