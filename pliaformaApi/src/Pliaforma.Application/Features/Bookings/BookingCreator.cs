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
    private readonly ContractLineEligibilityChecker _lineEligibility;
    private readonly ContractLineResolver _lineResolver;

    public BookingCreator(
        IAppDbContext db, IClassSessionCapacityService capacity, IDateTime clock,
        MakeupCreditService credits, IMakeupCreditLock creditLock,
        ContractLineEligibilityChecker lineEligibility, ContractLineResolver lineResolver)
    {
        _db = db;
        _capacity = capacity;
        _clock = clock;
        _credits = credits;
        _creditLock = creditLock;
        _lineEligibility = lineEligibility;
        _lineResolver = lineResolver;
    }

    /// <param name="studentContractId">Sprint 18a: header (back-compat) → bu header'ın uygun line'ı çözülür.</param>
    /// <param name="studentContractLineId">Sprint 28C-1: doğrudan line seçimi (staff line seçtiyse). null → resolver line bulur.</param>
    public async Task<ClassBooking> CreateAsync(
        Guid studioId, ClassSession session, StudentProfile student, Guid bookedByUserId, StudioRole bookerRole,
        CancellationToken ct, Guid? studentContractId = null, Guid? studentContractLineId = null)
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

        // Sprint 18a: öğrenci çakışması — aynı öğrenci örtüşen iki Booked derse giremez. (Sprint 21: ortak helper.)
        if (await SessionRules.StudentConflictAsync(_db, studioId, new[] { student.Id }, session.StartAt, session.EndAt, System.Array.Empty<Guid>(), ct))
            throw new ConflictException("Öğrencinin bu zaman aralığında başka bir dersi var.");

        // Sprint 27 (çift yönlü blok): öğrencinin bu aralıkta onay bekleyen telafi talebi varsa booking eklenemez.
        // (Approve yolu talebi önce Approved'a çevirdiğinden onaylanan talep burada Pending değildir → self-block yok.)
        if (await SessionRules.PendingStudentConflictAsync(_db, studioId, new[] { student.Id }, session.StartAt, session.EndAt, null, ct))
            throw new ConflictException("Öğrencinin bu zaman aralığında onay bekleyen bir telafi talebi var.");

        // Kullanılabilir-hak hesabı + makeup kredisi tüketimi için öğrenci satırını kilitle (serileştirme).
        await _creditLock.LockStudentAsync(studioId, student.Id, ct);

        // Sprint 28C-1: Regular ders LINE zorunlu (hak/tarih/hoca/bireysellik line'dan). Makeup → telafi kredisi yolu
        // (28C-1'de değişmez); Trial/Special → istisna (line gerektirmez). Uygun line yoksa → 409.
        Guid? contractId = null;
        Guid? contractLineId = null;

        if (session.SessionKind == ClassSessionKind.Regular)
        {
            var sessionDate = DateOnly.FromDateTime(session.StartAt);
            StudentContractLine line;
            if (studentContractLineId is { } lid)
            {
                var ce = await _lineEligibility.EvaluateAsync(studioId, lid, student.Id, session.ClassTypeId, sessionDate, session.InstructorProfileId, ct);
                if (!ce.IsEligible)
                    throw new ConflictException(ce.Message ?? "Sözleşme satırı bu rezervasyon için uygun değil.");
                line = ce.Line!;
            }
            else
            {
                // studentContractId verildiyse o header'ın uygun line'ı (back-compat); yoksa serbest çözüm.
                var resolved = await _lineResolver.ResolveAsync(studioId, student.Id, session.ClassTypeId, sessionDate, session.InstructorProfileId, ct, headerId: studentContractId);
                if (resolved is null)
                    throw new ConflictException("Bu ders için uygun aktif sözleşme satırı yok (hoca/ders tipi/tarih/hak). Önce satır oluşturun.");
                line = resolved;
            }

            // Sprint 28C-1 HARD RULE: bireysel line grup dersine atanamaz.
            if (line.IsIndividual && !session.IsIndividual)
                throw new ConflictException("Bireysel pakete (satır) sahip öğrenci grup dersine eklenemez; ayrı bireysel ders gerekir.");

            contractLineId = line.Id;
            contractId = line.StudentContractId;
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
            StudentContractLineId = contractLineId,
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
        // Sprint 27: KULLANILABİLİR bakiye (ledger − aktif Pending talep) — rezerve edilmiş kredi tekrar harcanamaz.
        // (Approve yolu talebi önce Approved'a çevirir → flip-first; bu yüzden onaylanan talep kendini bloklamaz.)
        var balance = await _credits.GetAvailableBalanceAsync(studioId, studentProfileId, ct);
        if (balance <= 0)
            throw new ConflictException("Telafi hakkı olmayan öğrenci telafi (makeup) dersine eklenemez.");
        _credits.Use(studioId, studentProfileId, 1, bookingId, bookedByUserId);
    }
}
