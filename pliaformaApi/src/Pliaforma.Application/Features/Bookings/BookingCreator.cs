using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Contracts;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Bookings;

/// <summary>
/// Staff ve self rezervasyonun ortak çekirdeği: seansın rezerve edilebilirliğini doğrular, paket
/// uygunluğu + kullanılabilir hakkı değerlendirir (Regular seans, Sprint 14), kapasiteyi ATOMİK tüketir,
/// MAKEUP seansında telafi kredisi düşer ve rezervasyonu ekler. Aktif transaction içinde çağrılır
/// (TransactionBehavior); öğrenci satırı kilitlenerek (row-lock) kullanılabilir-hak hesabı + booking
/// yaratma serileşir → iki eşzamanlı self-booking aynı hakkı iki kez rezerve edemez. Herhangi bir hata
/// rollback ile koltuk artışını ve kredi tüketimini geri alır.
/// </summary>
public sealed class BookingCreator
{
    private readonly IAppDbContext _db;
    private readonly IClassSessionCapacityService _capacity;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;
    private readonly IMakeupCreditLock _creditLock;
    private readonly ContractEligibilityChecker _contracts;
    private readonly ContractResolver _resolver;

    public BookingCreator(
        IAppDbContext db, IClassSessionCapacityService capacity, IDateTime clock,
        MakeupCreditService credits, IMakeupCreditLock creditLock,
        ContractEligibilityChecker contracts, ContractResolver resolver)
    {
        _db = db;
        _capacity = capacity;
        _clock = clock;
        _credits = credits;
        _creditLock = creditLock;
        _contracts = contracts;
        _resolver = resolver;
    }

    /// <param name="studentContractId">Sprint 18a: dolu → sözleşme yolu (paket yerine sözleşme uygunluğu).
    /// null → mevcut paket/booking yolu (geri uyum, bozulmaz).</param>
    public async Task<ClassBooking> CreateAsync(
        Guid studioId, ClassSession session, StudentProfile student, Guid bookedByUserId, StudioRole bookerRole,
        CancellationToken ct, Guid? studentContractId = null)
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

        // Sprint 18a: bireysel ders enforcement — bireysel seansa 2. öğrenci eklenemez.
        if (session.IsIndividual)
        {
            var alreadyHas = await _db.ClassBookings.AnyAsync(
                b => b.ClassSessionId == session.Id && b.Status == ClassBookingStatus.Booked, ct);
            if (alreadyHas)
                throw new ConflictException("Bu ders bireysel; başka öğrenci eklenemez (önce bireyselliği kaldırın).");
        }

        // Sprint 18a: öğrenci çakışması — aynı öğrenci örtüşen iki Booked derse giremez.
        var studentBusy = await _db.ClassBookings.AnyAsync(
            b => b.StudioId == studioId && b.StudentProfileId == student.Id && b.Status == ClassBookingStatus.Booked
                 && b.ClassSession!.Status == ClassSessionStatus.Scheduled
                 && b.ClassSession.StartAt < session.EndAt && session.StartAt < b.ClassSession.EndAt, ct);
        if (studentBusy)
            throw new ConflictException("Öğrencinin bu zaman aralığında başka bir dersi var.");

        // Kullanılabilir-hak hesabı + makeup kredisi tüketimi için öğrenci satırını kilitle (serileştirme).
        await _creditLock.LockStudentAsync(studioId, student.Id, ct);

        // Sprint 18b: Regular ders SÖZLEŞME zorunlu (paket gate kaldırıldı). Makeup → telafi kredisi yolu;
        // Trial/Special → istisna (sözleşme gerektirmez). Sözleşmesiz Regular → 409.
        Guid? contractId = null;

        if (session.SessionKind == ClassSessionKind.Regular)
        {
            var sessionDate = DateOnly.FromDateTime(session.StartAt);
            if (studentContractId is { } provided)
            {
                var ce = await _contracts.EvaluateAsync(studioId, provided, student.Id, session.ClassTypeId, sessionDate, ct);
                if (!ce.IsEligible)
                    throw new ConflictException(ce.Message ?? "Sözleşme bu rezervasyon için uygun değil.");
                contractId = provided;
            }
            else
            {
                var resolved = await _resolver.ResolveAsync(studioId, student.Id, session.ClassTypeId, sessionDate, ct);
                if (resolved is null)
                    throw new ConflictException("Bu ders için uygun aktif sözleşme yok. Önce sözleşme oluşturun.");
                contractId = resolved.Id;
            }
        }

        // Atomik koltuk rezervasyonu (BookedCount < Capacity ise artırır). Doluysa reddet.
        var reserved = await _capacity.TryReserveSeatAsync(studioId, session.Id, ct);
        if (!reserved)
            throw new ConflictException("Seans dolu; yeni rezervasyon yapılamaz.");

        var booking = new ClassBooking
        {
            StudioId = studioId,
            ClassSessionId = session.Id,
            StudentProfileId = student.Id,
            StudentContractId = contractId,
            Status = ClassBookingStatus.Booked,
            BookedByUserId = bookedByUserId,
            // Sprint 18b: paket alanları yeni Regular akışında kullanılmaz (dormant; her zaman null/false).
            ExpectedStudentPackageId = null,
            RequiresPayment = false,
            PackageEligibilityStatus = null,
        };

        // MAKEUP seansı (Sprint 20): yalnız Owner/StudioAdmin telafi dersine öğrenci ekleyebilir; STRICT
        // eligibility — telafi hakkı yoksa 409 (override YOK). Booking ile aynı transaction'da.
        if (session.SessionKind == ClassSessionKind.Makeup)
        {
            if (bookerRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
                throw new ForbiddenException("Telafi dersine öğrenci ekleme yetkisi yalnızca Owner/StudioAdmin'dedir.");
            await ConsumeMakeupCreditAsync(studioId, student.Id, booking.Id, bookedByUserId, ct);
        }

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
        Guid studioId, Guid studentProfileId, Guid bookingId, Guid bookedByUserId, CancellationToken ct)
    {
        // Sprint 20: STRICT — telafi hakkı yoksa kimse (Owner/Admin dâhil) telafi dersine ekleyemez.
        // `AllowMakeupBookingWithoutCreditByAdmin` politikası DORMANT (yeni akışta kullanılmaz). Öğrenci satırı
        // kilitli olduğundan (row-lock) son kredi iki eşzamanlı rezervasyonda iki kez harcanamaz.
        var balance = await _credits.GetBalanceAsync(studioId, studentProfileId, ct);
        if (balance <= 0)
            throw new ConflictException("Telafi hakkı olmayan öğrenci telafi (makeup) dersine eklenemez.");
        _credits.Use(studioId, studentProfileId, 1, bookingId, bookedByUserId);
    }
}
