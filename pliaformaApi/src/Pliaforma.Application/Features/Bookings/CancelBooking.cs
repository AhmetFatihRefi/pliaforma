using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>
/// Bir rezervasyonu iptal eder ve kapasiteyi geri açar. Student yalnızca KENDİ rezervasyonunu;
/// Owner/StudioAdmin herhangi birini; Instructor yalnızca KENDİ seansına ait olanı iptal eder.
/// Zaten iptal edilmişse 409. İptal eden role göre CancelledByStudent/CancelledByStudio.
/// </summary>
public sealed record CancelBookingCommand(Guid BookingId, string? Reason = null) : ICommand<Unit>, IStudioWriteRequest;

public sealed class CancelBookingHandler : ICommandHandler<CancelBookingCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClassSessionCapacityService _capacity;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;

    public CancelBookingHandler(IAppDbContext db, ICurrentUserService currentUser, IClassSessionCapacityService capacity, IDateTime clock, MakeupCreditService credits)
    {
        _db = db;
        _currentUser = currentUser;
        _capacity = capacity;
        _clock = clock;
        _credits = credits;
    }

    public async Task<Unit> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();
        var userId = _currentUser.RequireUserId();
        var role = _currentUser.StudioRole;

        var booking = await _db.ClassBookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId && b.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Rezervasyon", request.BookingId);

        if (booking.Status != ClassBookingStatus.Booked)
            throw new ConflictException("Bu rezervasyon zaten iptal edilmiş.");

        ClassBookingStatus newStatus;
        if (role == StudioRole.Student)
        {
            // Yalnızca kendi rezervasyonu.
            var ownsBooking = await _db.StudentProfiles
                .AnyAsync(s => s.Id == booking.StudentProfileId && s.UserId == userId, cancellationToken);
            if (!ownsBooking)
                throw new ForbiddenException("Yalnızca kendi rezervasyonunuzu iptal edebilirsiniz.");
            newStatus = ClassBookingStatus.CancelledByStudent;
        }
        else if (role is StudioRole.Owner or StudioRole.StudioAdmin)
        {
            newStatus = ClassBookingStatus.CancelledByStudio;
        }
        else if (role == StudioRole.Instructor)
        {
            var session = await _db.ClassSessions
                .FirstAsync(s => s.Id == booking.ClassSessionId, cancellationToken);
            var ownsSession = await _db.InstructorProfiles
                .AnyAsync(p => p.Id == session.InstructorProfileId && p.UserId == userId, cancellationToken);
            if (!ownsSession)
                throw new ForbiddenException("Yalnızca kendi seanslarınızdaki rezervasyonları iptal edebilirsiniz.");
            newStatus = ClassBookingStatus.CancelledByStudio;
        }
        else
        {
            throw new ForbiddenException("İptal yetkiniz yok.");
        }

        booking.Status = newStatus;
        booking.CancelledByUserId = userId;
        booking.CancelledAt = _clock.UtcNow;
        booking.CancellationReason = request.Reason?.Trim();

        await _capacity.ReleaseSeatAsync(studioId, booking.ClassSessionId, cancellationToken);

        // MAKEUP rezervasyonu iptal edildiyse, tüketilen krediyi geri ver (Restored). Yalnızca bu booking
        // için bir Used hareketi varsa ve henüz geri verilmediyse. Booking yeniden iptal edilemediği
        // (status guard) için çift iade oluşmaz.
        // Sprint 28C-2: iade tüketimin yapıldığı LINE'a yazılır (Used tx'in StudentContractLineId'si).
        var usedTx = await _db.MakeupCreditTransactions
            .Where(t => t.StudioId == studioId && t.UsedForBookingId == booking.Id && t.Type == MakeupCreditTransactionType.Used)
            .Select(t => new { t.StudentContractLineId }).FirstOrDefaultAsync(cancellationToken);
        if (usedTx is not null && usedTx.StudentContractLineId is { } restoreLineId)
        {
            var alreadyRestored = await _db.MakeupCreditTransactions.AnyAsync(
                t => t.StudioId == studioId && t.SourceBookingId == booking.Id
                     && t.Type == MakeupCreditTransactionType.Restored, cancellationToken);
            if (!alreadyRestored)
                _credits.Restore(studioId, booking.StudentProfileId, restoreLineId, 1, booking.Id, userId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
