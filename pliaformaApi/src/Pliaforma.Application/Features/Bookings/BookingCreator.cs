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

            // Sprint 33 HARD RULE: kapasite tercihi varsa, kapasitesi tercihi aşan derse eklenemez (bireysel=1 özel hâl).
            if (line.PreferredMaxCapacity is { } cap && session.Capacity > cap)
                throw new ConflictException(cap == 1
                    ? $"Bireysel ders tercihli sözleşme grup dersine (kapasite {session.Capacity}) eklenemez."
                    : $"Bu sözleşmenin kapasite tercihi en fazla {cap} kişi; daha büyük bir derse (kapasite {session.Capacity}) eklenemez.");

            contractLineId = line.Id;
            contractId = line.StudentContractId;
        }

        // MAKEUP seansı (Sprint 20 + 28C-2 LINE-bazlı): yalnız Owner/StudioAdmin; telafi LINE'dan tüketilir.
        // Line classType == session.classType olmalı (Reformer telafisi Mat dersine harcanmaz). Booking ile aynı transaction.
        StudentContractLine? makeupLine = null;
        if (session.SessionKind == ClassSessionKind.Makeup)
        {
            if (bookerRole is not (StudioRole.Owner or StudioRole.StudioAdmin))
                throw new ForbiddenException("Telafi dersine öğrenci ekleme yetkisi yalnızca Owner/StudioAdmin'dedir.");
            makeupLine = await ResolveMakeupLineAsync(studioId, student.Id, session.ClassTypeId, studentContractLineId, ct);
            contractLineId = makeupLine.Id;
            contractId = makeupLine.StudentContractId;
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

        // Telafi tüketimi: o LINE'dan Use(−1) (kredi kilit altında serileşir; booking.Id usedFor olarak izlenir).
        if (makeupLine is not null)
            _credits.Use(studioId, student.Id, makeupLine.Id, 1, booking.Id, bookedByUserId);

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

    /// <summary>
    /// Telafi dersine tüketilecek LINE'ı çözer (Sprint 28C-2 — karar #3). Aday line: aynı öğrenci + Active +
    /// <c>line.ClassTypeId == session.ClassTypeId</c> + line KULLANILABİLİR telafi hakkı > 0. lineId verildiyse
    /// doğrula; verilmediyse uygun line'ları bul → TEK ise fallback, ÇOKLU ise 409 (line seçimi gerekli).
    /// </summary>
    private async Task<StudentContractLine> ResolveMakeupLineAsync(
        Guid studioId, Guid studentProfileId, Guid classTypeId, Guid? explicitLineId, CancellationToken ct)
    {
        if (explicitLineId is { } lid)
        {
            var line = await _db.StudentContractLines.Include(l => l.StudentContract)
                .FirstOrDefaultAsync(l => l.Id == lid && l.StudioId == studioId, ct)
                ?? throw new NotFoundException("Sözleşme satırı", lid);
            if (line.StudentContract!.StudentProfileId != studentProfileId)
                throw new ConflictException("Seçilen satır bu öğrenciye ait değil.");
            if (line.Status != StudentContractLineStatus.Active)
                throw new ConflictException("Seçilen satır aktif değil.");
            if (line.ClassTypeId != classTypeId)
                throw new ConflictException("Telafi satırının ders tipi bu derse uymuyor.");
            if (await _credits.GetLineAvailableBalanceAsync(studioId, line.Id, ct) <= 0)
                throw new ConflictException("Seçilen satırda kullanılabilir telafi hakkı yok.");
            return line;
        }

        var candidates = await _db.StudentContractLines.Include(l => l.StudentContract)
            .Where(l => l.StudioId == studioId && l.StudentContract!.StudentProfileId == studentProfileId
                        && l.ClassTypeId == classTypeId && l.Status == StudentContractLineStatus.Active)
            .ToListAsync(ct);
        var eligible = new List<StudentContractLine>();
        foreach (var l in candidates)
            if (await _credits.GetLineAvailableBalanceAsync(studioId, l.Id, ct) > 0)
                eligible.Add(l);

        if (eligible.Count == 0)
            throw new ConflictException("Telafi hakkı olan uygun satır yok (ders tipi/kredi). Telafi dersine eklenemez.");
        if (eligible.Count > 1)
            throw new ConflictException("Birden fazla uygun telafi satırı var; hangi satırdan düşüleceğini seçin.");
        return eligible[0];
    }
}
