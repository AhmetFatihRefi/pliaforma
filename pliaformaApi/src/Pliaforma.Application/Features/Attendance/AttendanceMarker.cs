using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>
/// Yoklama işaretlemenin ortak çekirdeği (batch + single tarafından paylaşılır). Her aktif (Booked)
/// rezervasyon için Attendance kaydını **upsert** eder (ikinci işaretleme update'tir, duplicate oluşmaz).
/// Mazeretli/Geç iptal + grant istenirse — ve bu yoklamadan daha önce kredi verilmemişse — 1 telafi
/// kredisi verir. Sprint 19: <b>devamsızlık (Absent)</b> sözleşme <c>MakeupAllowance</c> limitiyle telafi
/// üretir; durum Absent'tan çıkınca ters kayıtla (immutable ledger) dengelenir. Paket tüketimi YOKTUR
/// (Regular = sözleşme). SaveChanges ÇAĞIRMAZ — çağıranın transaction'ında. Zaman/yetki kontrolü çağıranda.
/// </summary>
public sealed class AttendanceMarker
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;

    public AttendanceMarker(IAppDbContext db, IDateTime clock, MakeupCreditService credits)
    {
        _db = db;
        _clock = clock;
        _credits = credits;
    }

    /// <summary>İşaretlemeyi uygular; yeni verilen (manuel) kredi sayısını döner.</summary>
    public async Task<int> MarkAsync(
        Guid studioId, ClassSession session, Guid markedByUserId, StudioRole? role,
        IReadOnlyList<AttendanceMarkInput> items, CancellationToken ct)
    {
        // Manuel grant (Excused/LateCancel) ya da devamsızlık (Absent) → politika/expiry gerekir.
        var wantsManualGrant = items.Any(i => i.GrantMakeupCredit
            && (i.Status == AttendanceStatus.Excused || i.Status == AttendanceStatus.LateCancel));
        var touchesAbsence = items.Any(i => i.Status == AttendanceStatus.Absent) || items.Count > 0;

        StudioMakeupPolicy? policy = null;
        if (wantsManualGrant || touchesAbsence)
        {
            policy = await _credits.GetPolicyAsync(studioId, ct);
            if (wantsManualGrant && role == StudioRole.Instructor && !policy.AllowInstructorManualGrant)
                throw new ForbiddenException("Politika gereği hocalar yoklama üzerinden telafi kredisi veremez.");
        }
        var expiresAt = policy is null ? null : _credits.ComputeExpiry(policy);

        var bookingIds = items.Select(i => i.BookingId).Distinct().ToList();

        // Yalnızca bu seansa ait, aynı stüdyodaki rezervasyonlar (tenant + tutarlılık).
        var bookings = await _db.ClassBookings
            .Where(b => b.StudioId == studioId && b.ClassSessionId == session.Id && bookingIds.Contains(b.Id))
            .ToListAsync(ct);
        var bookingById = bookings.ToDictionary(b => b.Id);

        var existing = await _db.Attendances
            .Where(a => a.StudioId == studioId && bookingIds.Contains(a.ClassBookingId))
            .ToListAsync(ct);
        var attByBooking = existing.ToDictionary(a => a.ClassBookingId);

        // Batch içinde aynı sözleşmeye birden çok devamsızlık → limit aşımını önlemek için kayıt-içi sayaç.
        var pendingByContract = new Dictionary<Guid, int>();

        var credited = 0;
        foreach (var item in items)
        {
            if (!bookingById.TryGetValue(item.BookingId, out var booking))
                throw new NotFoundException("Rezervasyon", item.BookingId); // bu seansta yok / başka stüdyo
            if (booking.Status != ClassBookingStatus.Booked)
                throw new BusinessRuleException("İptal edilmiş rezervasyon için yoklama alınamaz.");

            if (!attByBooking.TryGetValue(item.BookingId, out var att))
            {
                att = new Domain.Entities.Attendance
                {
                    StudioId = studioId,
                    ClassBookingId = booking.Id,
                    ClassSessionId = session.Id,
                    StudentProfileId = booking.StudentProfileId,
                };
                _db.Attendances.Add(att);
                attByBooking[item.BookingId] = att;
            }

            att.Status = item.Status;
            att.Notes = item.Notes?.Trim();
            att.MarkedByUserId = markedByUserId;
            att.MarkedAt = _clock.UtcNow;

            // Mazeretli/Geç iptal + grant + bu yoklamadan henüz kredi verilmediyse → +1 telafi kredisi (manuel).
            if (item.GrantMakeupCredit
                && (item.Status == AttendanceStatus.Excused || item.Status == AttendanceStatus.LateCancel)
                && att.MakeupCreditTransactionId is null)
            {
                var reason = item.Status == AttendanceStatus.Excused
                    ? MakeupCreditReason.ExcusedAbsence
                    : MakeupCreditReason.NoShowApproved;
                var tx = _credits.Grant(studioId, booking.StudentProfileId, 1, reason, markedByUserId,
                    sourceSessionId: session.Id, sourceBookingId: booking.Id, expiresAt: expiresAt);
                att.MakeupCreditTransactionId = tx.Id;
                credited++;
            }

            // Devamsızlık → sözleşme limitli telafi (immutable ledger; Absent'tan çıkınca ters kayıt).
            await SyncAbsenceCreditAsync(studioId, booking, item.Status, markedByUserId, expiresAt, pendingByContract, ct);
        }

        return credited;
    }

    /// <summary>
    /// Devamsızlık telafisi defter senkronu (yalnız sözleşme dersi). Booking başına net en fazla 1
    /// <see cref="MakeupCreditReason.AbsentEarned"/>; sözleşme <c>MakeupAllowance</c> ile sınırlı; idempotent;
    /// Absent→Present/diğer geçişte ters kayıt (Revoked), tekrar Absent'ta Restored. SourceClassSessionId
    /// yazılmaz → (StudentProfileId, SourceClassSessionId) Granted-unique index'inden bağımsızdır.
    /// </summary>
    private async Task SyncAbsenceCreditAsync(
        Guid studioId, ClassBooking booking, AttendanceStatus status, Guid byUserId, DateTime? expiresAt,
        Dictionary<Guid, int> pendingByContract, CancellationToken ct)
    {
        if (booking.StudentContractId is not { } contractId) return; // yalnız sözleşme (Regular) dersi

        var isAbsent = status == AttendanceStatus.Absent;

        var bookingTxs = await _db.MakeupCreditTransactions
            .Where(t => t.StudioId == studioId && t.SourceBookingId == booking.Id
                        && t.Reason == MakeupCreditReason.AbsentEarned)
            .ToListAsync(ct);
        var bookingNet = bookingTxs.Sum(t => t.Amount);
        var hasGranted = bookingTxs.Any(t => t.Type == MakeupCreditTransactionType.Granted);

        if (isAbsent)
        {
            if (bookingNet > 0) return; // zaten var (idempotent — çift telafi yok)

            // Sözleşme limiti: bu sözleşmenin tüm booking'lerindeki net AbsentEarned (+ bu çağrıda eklenen pending).
            var contractBookingIds = await _db.ClassBookings
                .Where(b => b.StudioId == studioId && b.StudentContractId == contractId)
                .Select(b => b.Id).ToListAsync(ct);
            var contractNetDb = await _db.MakeupCreditTransactions
                .Where(t => t.StudioId == studioId && t.Reason == MakeupCreditReason.AbsentEarned
                            && t.SourceBookingId != null && contractBookingIds.Contains(t.SourceBookingId.Value))
                .SumAsync(t => (int?)t.Amount, ct) ?? 0;
            var allowance = await _db.StudentContracts
                .Where(c => c.Id == contractId).Select(c => c.MakeupAllowance).FirstAsync(ct);

            if (contractNetDb + pendingByContract.GetValueOrDefault(contractId) >= allowance)
                return; // limit dolu → devamsızlık kaydedilir ama telafi verilmez

            // İlk kez → Granted; daha önce Granted olup Revoked edilmişse → Restored (Granted-unique'i ihlal etmez).
            _credits.AddAbsenceLedger(studioId, booking.StudentProfileId, +1,
                hasGranted ? MakeupCreditTransactionType.Restored : MakeupCreditTransactionType.Granted,
                byUserId, booking.Id, hasGranted ? null : expiresAt);
            pendingByContract[contractId] = pendingByContract.GetValueOrDefault(contractId) + 1;
        }
        else
        {
            if (bookingNet <= 0) return; // yok (idempotent)
            _credits.AddAbsenceLedger(studioId, booking.StudentProfileId, -1,
                MakeupCreditTransactionType.Revoked, byUserId, booking.Id, expiresAt: null);
            pendingByContract[contractId] = pendingByContract.GetValueOrDefault(contractId) - 1;
        }
    }
}
