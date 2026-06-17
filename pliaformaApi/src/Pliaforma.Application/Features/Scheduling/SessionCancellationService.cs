using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Scheduling;

/// <summary>
/// Bir seansın aktif rezervasyonlarını iptal etmenin ortak çekirdeği (tekil seans iptali + toplu
/// closure tarafından paylaşılır). Tüketilmiş telafi kredilerini geri verir (öğrenci stüdyo iptalinden
/// dolayı kredi kaybetmez) ve istenirse telafi kredisi tahsis eder (seans başına öğrenciye bir kez).
/// SaveChanges ÇAĞIRMAZ — çağıranın transaction'ı içinde çalışır.
/// </summary>
public sealed class SessionCancellationService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;

    public SessionCancellationService(IAppDbContext db, IDateTime clock, MakeupCreditService credits)
    {
        _db = db;
        _clock = clock;
        _credits = credits;
    }

    /// <summary>
    /// Seansın aktif (Booked) rezervasyonlarını CancelledByStudio yapar. Her booking için tek bir kredi
    /// sonucu uygulanır (çift kredi YOK):
    /// <list type="bullet">
    /// <item>Booking makeup kredisi tükettiyse → yalnızca o kredi geri verilir (Restored). Ekstra grant YOK.</item>
    /// <item>Kredi tüketmemiş booking (ör. Regular seans) ise ve <paramref name="grantMakeupCredit"/> true ise
    /// → +1 telafi kredisi tahsis edilir (seans+öğrenci için bir kez).</item>
    /// </list>
    /// (cancelled, credited) sayılarını döner; credited yalnızca yeni tahsis edilen grant'leri sayar.
    /// SaveChanges ÇAĞIRMAZ — çağıranın transaction'ı içinde çalışır.
    /// </summary>
    public async Task<(int Cancelled, int Credited)> CancelActiveBookingsAsync(
        Guid studioId, Guid sessionId, Guid byUserId, string reason,
        bool grantMakeupCredit, MakeupCreditReason grantReason, CancellationToken ct)
    {
        var activeBookings = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == sessionId && b.Status == ClassBookingStatus.Booked)
            .ToListAsync(ct);

        if (activeBookings.Count == 0)
            return (0, 0);

        var policy = grantMakeupCredit ? await _credits.GetPolicyAsync(studioId, ct) : null;
        var expiresAt = policy is null ? null : _credits.ComputeExpiry(policy);

        var credited = 0;
        foreach (var b in activeBookings)
        {
            b.Status = ClassBookingStatus.CancelledByStudio;
            b.CancelledByUserId = byUserId;
            b.CancelledAt = _clock.UtcNow;
            b.CancellationReason = reason;

            // Booking makeup kredisi tükettiyse: SADECE iade et (restore). Bu durumda ekstra grant verilmez.
            var consumedMakeupCredit = await RestoreConsumedMakeupCreditIfAnyAsync(studioId, b.Id, b.StudentProfileId, byUserId, ct);

            // Kredi tüketmemiş booking (Regular seans) için istenirse stüdyo iptali telafi kredisi ver.
            if (grantMakeupCredit && !consumedMakeupCredit
                && await GrantCreditForStudioCancellationIfEligibleAsync(studioId, sessionId, b, byUserId, grantReason, expiresAt, ct))
            {
                credited++;
            }
        }

        return (activeBookings.Count, credited);
    }

    /// <summary>
    /// Booking makeup kredisi tükettiyse (Used hareketi varsa) o krediyi geri verir (Restored, çift iade
    /// engelli) ve <c>true</c> döner. Tüketim yoksa hiçbir şey yapmaz, <c>false</c> döner. Dönen değer
    /// "bu booking bir telafi kredisi tüketmiş midir" sorusunun yanıtıdır → çağıran ekstra grant'ı buna göre
    /// engeller.
    /// </summary>
    private async Task<bool> RestoreConsumedMakeupCreditIfAnyAsync(Guid studioId, Guid bookingId, Guid studentProfileId, Guid byUserId, CancellationToken ct)
    {
        var usedCredit = await _db.MakeupCreditTransactions.AnyAsync(
            t => t.StudioId == studioId && t.UsedForBookingId == bookingId
                 && t.Type == MakeupCreditTransactionType.Used, ct);
        if (!usedCredit)
            return false;

        var alreadyRestored = await _db.MakeupCreditTransactions.AnyAsync(
            t => t.StudioId == studioId && t.SourceBookingId == bookingId
                 && t.Type == MakeupCreditTransactionType.Restored, ct);
        if (!alreadyRestored)
            _credits.Restore(studioId, studentProfileId, 1, bookingId, byUserId);

        return true;
    }

    /// <summary>
    /// Stüdyo iptali için telafi kredisi (Granted +1) tahsis eder — yalnızca bu seans+öğrenci için daha
    /// önce verilmemişse. Tahsis edildiyse <c>true</c> döner. Yalnızca kredi TÜKETMEMİŞ booking'ler için
    /// çağrılmalıdır (çift kredi engeli çağırandadır).
    /// </summary>
    private async Task<bool> GrantCreditForStudioCancellationIfEligibleAsync(
        Guid studioId, Guid sessionId, ClassBooking booking, Guid byUserId, MakeupCreditReason grantReason, DateTime? expiresAt, CancellationToken ct)
    {
        var already = await _credits.HasGrantForSessionAsync(studioId, booking.StudentProfileId, sessionId, ct);
        if (already)
            return false;

        _credits.Grant(studioId, booking.StudentProfileId, 1, grantReason, byUserId,
            sourceSessionId: sessionId, sourceBookingId: booking.Id, expiresAt: expiresAt);
        return true;
    }
}
