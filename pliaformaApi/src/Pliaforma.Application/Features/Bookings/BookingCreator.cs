using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>
/// Staff ve self rezervasyonun ortak çekirdeği: seansın rezerve edilebilirliğini doğrular,
/// kapasiteyi ATOMİK tüketir, MAKEUP seansında telafi kredisi düşer ve rezervasyonu ekler. Aktif
/// transaction içinde çağrılır (TransactionBehavior); herhangi bir hata rollback ile hem koltuk
/// artışını hem kredi tüketimini geri alır → "kredi düştü ama booking patladı" durumu oluşmaz.
/// </summary>
public sealed class BookingCreator
{
    private readonly IAppDbContext _db;
    private readonly IClassSessionCapacityService _capacity;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;
    private readonly IMakeupCreditLock _creditLock;

    public BookingCreator(
        IAppDbContext db, IClassSessionCapacityService capacity, IDateTime clock,
        MakeupCreditService credits, IMakeupCreditLock creditLock)
    {
        _db = db;
        _capacity = capacity;
        _clock = clock;
        _credits = credits;
        _creditLock = creditLock;
    }

    public async Task<ClassBooking> CreateAsync(
        Guid studioId, ClassSession session, StudentProfile student, Guid bookedByUserId, StudioRole bookerRole, CancellationToken ct)
    {
        if (session.Status != ClassSessionStatus.Scheduled)
            throw new BusinessRuleException("Yalnızca planlı (Scheduled) seanslara rezervasyon yapılabilir.");

        var nowLocal = await SessionRules.StudioNowAsync(_db, _clock, studioId, ct);
        if (session.StartAt <= nowLocal)
            throw new BusinessRuleException("Geçmiş seansa rezervasyon yapılamaz.");

        // Friendly çift-rezervasyon ön kontrolü (race koruması filtered unique index'tedir).
        var dup = await _db.ClassBookings.AnyAsync(
            b => b.ClassSessionId == session.Id && b.StudentProfileId == student.Id && b.Status == ClassBookingStatus.Booked, ct);
        if (dup)
            throw new ConflictException("Bu öğrencinin bu seansta zaten aktif bir rezervasyonu var.");

        // Atomik koltuk rezervasyonu (BookedCount < Capacity ise artırır). Doluysa reddet.
        var reserved = await _capacity.TryReserveSeatAsync(studioId, session.Id, ct);
        if (!reserved)
            throw new ConflictException("Seans dolu; yeni rezervasyon yapılamaz.");

        var booking = new ClassBooking
        {
            StudioId = studioId,
            ClassSessionId = session.Id,
            StudentProfileId = student.Id,
            Status = ClassBookingStatus.Booked,
            BookedByUserId = bookedByUserId,
        };

        // MAKEUP seansı: 1 telafi kredisi tüket. Booking ile aynı transaction'da (rollback ikisini de geri alır).
        if (session.SessionKind == ClassSessionKind.Makeup)
            await ConsumeMakeupCreditAsync(studioId, student.Id, booking.Id, bookedByUserId, bookerRole, ct);

        _db.ClassBookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Yarış: filtered unique index çift-rezervasyonu yakaladı. Rethrow → transaction rollback
            // koltuk artışını VE kredi tüketimini de geri alır.
            throw new ConflictException("Bu öğrencinin bu seansta zaten aktif bir rezervasyonu var.");
        }

        return booking;
    }

    private async Task ConsumeMakeupCreditAsync(
        Guid studioId, Guid studentProfileId, Guid bookingId, Guid bookedByUserId, StudioRole bookerRole, CancellationToken ct)
    {
        // Son krediyi iki eşzamanlı rezervasyonun birden harcamasını engelle: öğrenci satırını kilitle.
        await _creditLock.LockStudentAsync(studioId, studentProfileId, ct);
        var balance = await _credits.GetBalanceAsync(studioId, studentProfileId, ct);

        if (balance > 0)
        {
            _credits.Use(studioId, studentProfileId, 1, bookingId, bookedByUserId);
            return;
        }

        // Kredi yok. Politika izin veriyorsa ve booker Owner/StudioAdmin ise kredisiz override (tüketim yok).
        var policy = await _credits.GetPolicyAsync(studioId, ct);
        var staffOverride = policy.AllowMakeupBookingWithoutCreditByAdmin
                            && bookerRole is StudioRole.Owner or StudioRole.StudioAdmin;
        if (!staffOverride)
            throw new ConflictException("Telafi krediniz olmadığı için telafi (makeup) dersine rezervasyon yapılamaz.");
    }
}
