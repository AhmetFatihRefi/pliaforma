using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Contracts;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Packages;
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
    private readonly PackageEligibilityChecker _eligibility;
    private readonly ContractEligibilityChecker _contracts;

    public BookingCreator(
        IAppDbContext db, IClassSessionCapacityService capacity, IDateTime clock,
        MakeupCreditService credits, IMakeupCreditLock creditLock, PackageEligibilityChecker eligibility,
        ContractEligibilityChecker contracts)
    {
        _db = db;
        _capacity = capacity;
        _clock = clock;
        _credits = credits;
        _creditLock = creditLock;
        _eligibility = eligibility;
        _contracts = contracts;
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

        Guid? expectedPackageId = null;
        bool requiresPayment = false;
        PackageEligibilityStatus? eligStatus = null;
        Guid? contractId = studentContractId;

        if (session.SessionKind == ClassSessionKind.Regular)
        {
            if (studentContractId is { } cid)
            {
                // SÖZLEŞME YOLU (Sprint 18a): paket yerine sözleşme uygunluğu.
                var ce = await _contracts.EvaluateAsync(studioId, cid, student.Id, session.ClassTypeId, DateOnly.FromDateTime(session.StartAt), ct);
                if (!ce.IsEligible)
                    throw new ConflictException(ce.Message ?? "Sözleşme bu rezervasyon için uygun değil.");
            }
            else
            {
                // ESKİ PAKET YOLU (değişmedi).
                var today = DateOnly.FromDateTime(nowLocal);
                var elig = await _eligibility.EvaluateAsync(studioId, student.Id, session.ClassTypeId, today, ct);
                if (elig.IsEligible)
                {
                    expectedPackageId = elig.ExpectedStudentPackageId;
                    eligStatus = PackageEligibilityStatus.Eligible;
                }
                else if (bookerRole is StudioRole.Owner or StudioRole.StudioAdmin)
                {
                    requiresPayment = true;
                    eligStatus = PackageEligibilityStatus.RequiresPayment;
                }
                else
                {
                    throw new ConflictException(elig.Message);
                }
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
            ExpectedStudentPackageId = expectedPackageId,
            RequiresPayment = requiresPayment,
            PackageEligibilityStatus = eligStatus,
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
        // Son krediyi iki eşzamanlı rezervasyonun birden harcamasını engelle: öğrenci satırı zaten kilitli.
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
