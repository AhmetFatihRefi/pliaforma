using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>Ders seansı iş kuralları: stüdyo-yerel "şimdi", çakışma, müsaitlik penceresi.</summary>
internal static class SessionRules
{
    /// <summary>
    /// Stüdyo zaman dilimine göre şimdiki yerel saat (duvar saati, Unspecified). Geçersiz/çözülemeyen TZ →
    /// Türkiye saatine düşer (UTC'ye ASLA düşmez; aksi halde program mantığı 3 saat kayardı).
    /// </summary>
    public static async Task<DateTime> StudioNowAsync(IAppDbContext db, IDateTime clock, Guid studioId, CancellationToken ct)
    {
        var tzId = await db.Studios.Where(s => s.Id == studioId).Select(s => s.TimeZoneId).FirstAsync(ct);
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = Common.Time.TurkeyTime.Zone; }
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, tz), DateTimeKind.Unspecified);
    }

    /// <summary>Verilen aralık için aktif (Scheduled) çakışan seans var mı? (excludeId hariç).</summary>
    public static Task<bool> HasConflictAsync(
        IAppDbContext db, Guid studioId, Guid instructorProfileId, DateTime startAt, DateTime endAt,
        Guid? excludeId, CancellationToken ct) =>
        db.ClassSessions.AnyAsync(
            s => s.StudioId == studioId
                 && s.InstructorProfileId == instructorProfileId
                 && s.Status == ClassSessionStatus.Scheduled
                 && (excludeId == null || s.Id != excludeId)
                 && s.StartAt < endAt && startAt < s.EndAt,
            ct);

    /// <summary>
    /// Verilen öğrencilerden herhangi biri, <paramref name="excludeSessionIds"/> dışındaki örtüşen bir
    /// Scheduled derste Booked mu? (Sprint 18a öğrenci-çakışması; Sprint 21 exclude'lu — taşıma/zincirde
    /// kayan seanslar kendi öğrenci çakışmalarını tetiklemesin diye). BookingCreator + reschedule + chain-shift ortak.
    /// </summary>
    public static Task<bool> StudentConflictAsync(
        IAppDbContext db, Guid studioId, IReadOnlyCollection<Guid> studentProfileIds,
        DateTime startAt, DateTime endAt, IReadOnlyCollection<Guid> excludeSessionIds, CancellationToken ct) =>
        db.ClassBookings.AnyAsync(
            b => b.StudioId == studioId
                 && studentProfileIds.Contains(b.StudentProfileId)
                 && b.Status == ClassBookingStatus.Booked
                 && b.ClassSession!.Status == ClassSessionStatus.Scheduled
                 && !excludeSessionIds.Contains(b.ClassSessionId)
                 && b.ClassSession.StartAt < endAt && startAt < b.ClassSession.EndAt,
            ct);

    /// <summary>
    /// Sprint 27 (çift yönlü blok): aynı HOCA için verilen aralıkta onay bekleyen (Pending) bir telafi talebi var mı?
    /// (yalnız Pending bloklar; Rejected/Withdrawn/Approved DAHİL DEĞİL — Approved zaten gerçek seans olarak ayrıca
    /// taranır). <paramref name="excludeRequestId"/> approve sırasında onaylanan talebin kendini bloklamaması için.
    /// </summary>
    public static Task<bool> PendingInstructorConflictAsync(
        IAppDbContext db, Guid studioId, Guid instructorProfileId, DateTime startAt, DateTime endAt,
        Guid? excludeRequestId, CancellationToken ct) =>
        db.MakeupRequests.AnyAsync(
            r => r.StudioId == studioId
                 && r.RequestedInstructorProfileId == instructorProfileId
                 && r.Status == MakeupRequestStatus.Pending
                 && (excludeRequestId == null || r.Id != excludeRequestId)
                 && r.StartAt < endAt && startAt < r.EndAt, ct);

    /// <summary>
    /// Sprint 27 (çift yönlü blok): verilen ÖĞRENCİLERDEN biri için verilen aralıkta onay bekleyen (Pending) bir telafi
    /// talebi var mı? (yalnız Pending bloklar). <paramref name="excludeRequestId"/> approve self-block önler.
    /// </summary>
    public static Task<bool> PendingStudentConflictAsync(
        IAppDbContext db, Guid studioId, IReadOnlyCollection<Guid> studentProfileIds, DateTime startAt, DateTime endAt,
        Guid? excludeRequestId, CancellationToken ct) =>
        db.MakeupRequests.AnyAsync(
            r => r.StudioId == studioId
                 && studentProfileIds.Contains(r.StudentProfileId)
                 && r.Status == MakeupRequestStatus.Pending
                 && (excludeRequestId == null || r.Id != excludeRequestId)
                 && r.StartAt < endAt && startAt < r.EndAt, ct);

    /// <summary>
    /// Taşıma (reschedule / chain-shift) HARD kuralları (ihlalde fırlatır): geçmiş zaman, HOCA çakışması
    /// ve ÖĞRENCİ çakışması (ikisi de <paramref name="excludeSessionIds"/> dışındaki Scheduled seanslara karşı).
    /// Sprint 27: ayrıca PENDING telafi talebi blokları (hoca + öğrenci) da zorlanır → pending gerçekten saati bloklar.
    /// Hoca MÜSAİTLİĞİ burada ZORLANMAZ (Sprint 21 kararı: availability ADVISORY) — çağıran uyarı olarak gösterir.
    /// Bireysellik/kapasite taşımada değişmez (öğrenci sayısı aynı kalır) → kontrol edilmez.
    /// </summary>
    public static async Task EnsureMovableAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, Guid instructorProfileId,
        IReadOnlyCollection<Guid> studentProfileIds, DateTime startAt, DateTime endAt,
        IReadOnlyCollection<Guid> excludeSessionIds, CancellationToken ct)
    {
        var nowLocal = await StudioNowAsync(db, clock, studioId, ct);
        if (startAt < nowLocal)
            throw new BusinessRuleException("Geçmiş bir zamana ders taşınamaz.");

        var instructorConflict = await db.ClassSessions.AnyAsync(
            s => s.StudioId == studioId && s.InstructorProfileId == instructorProfileId
                 && s.Status == ClassSessionStatus.Scheduled
                 && !excludeSessionIds.Contains(s.Id)
                 && s.StartAt < endAt && startAt < s.EndAt, ct);
        if (instructorConflict)
            throw new ConflictException("Hoca bu zaman aralığında başka bir derse atanmış.");

        if (studentProfileIds.Count > 0
            && await StudentConflictAsync(db, studioId, studentProfileIds, startAt, endAt, excludeSessionIds, ct))
            throw new ConflictException("Öğrencinin bu zaman aralığında başka bir dersi var.");

        if (await PendingInstructorConflictAsync(db, studioId, instructorProfileId, startAt, endAt, null, ct))
            throw new ConflictException("Hoca bu zaman aralığında onay bekleyen bir telafi talebine atanmış.");

        if (studentProfileIds.Count > 0
            && await PendingStudentConflictAsync(db, studioId, studentProfileIds, startAt, endAt, null, ct))
            throw new ConflictException("Öğrencinin bu zaman aralığında onay bekleyen bir telafi talebi var.");
    }

    /// <summary>
    /// Tek seans oluşturma/güncellemede HARD kuralları zorlar (hata fırlatır): geçmiş-zaman + hoca-çakışma.
    /// Sprint 26: hoca "müsaitlik penceresi" kontrolü KALDIRILDI — sistem availability'ye göre engel koymaz;
    /// hocanın boş/dolu saatleri ders programından anlaşılır. Yalnız gerçek çakışma + geçmiş engellenir.
    /// Sprint 42: <paramref name="allowPast"/> true ise YALNIZ geçmiş-zaman engeli atlanır (manuel backfill,
    /// Owner/StudioAdmin + UI onayı); çakışma ve pending-telafi blokları HER DURUMDA zorlanmaya devam eder.
    /// </summary>
    public static async Task EnsureValidAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, Guid instructorProfileId,
        DateTime startAt, DateTime endAt, Guid? excludeId, CancellationToken ct, bool allowPast = false)
    {
        var nowLocal = await StudioNowAsync(db, clock, studioId, ct);
        if (!allowPast && startAt < nowLocal)
            throw new BusinessRuleException("Geçmiş bir zamana ders oluşturulamaz.");

        if (await HasConflictAsync(db, studioId, instructorProfileId, startAt, endAt, excludeId, ct))
            throw new BusinessRuleException("Hoca bu zaman aralığında başka bir derse atanmış.");

        // Sprint 27 (çift yönlü blok): pending bir telafi talebinin saatine gerçek ders oluşturulamaz (hoca tarafı).
        if (await PendingInstructorConflictAsync(db, studioId, instructorProfileId, startAt, endAt, null, ct))
            throw new BusinessRuleException("Hoca bu zaman aralığında onay bekleyen bir telafi talebine atanmış.");
    }
}
