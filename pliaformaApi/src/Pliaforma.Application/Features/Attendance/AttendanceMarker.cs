using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.MakeupCredits;
using Pliaforma.Application.Features.Packages;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Attendance;

/// <summary>
/// Yoklama işaretlemenin ortak çekirdeği (batch + single tarafından paylaşılır). Her aktif (Booked)
/// rezervasyon için Attendance kaydını **upsert** eder (ikinci işaretleme update'tir, duplicate oluşmaz).
/// Mazeretli/Geç iptal + grant istenirse — ve bu yoklamadan daha önce kredi verilmemişse — 1 telafi
/// kredisi verir (yoklama ile aynı transaction'da). SaveChanges ÇAĞIRMAZ — çağıranın transaction'ında.
/// Zaman/yetki kontrolü (gelecek seans, seans iptal, hoca sahipliği) ÇAĞIRANDA yapılır.
/// </summary>
public sealed class AttendanceMarker
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly MakeupCreditService _credits;
    private readonly StudentPackageConsumer _packages;

    public AttendanceMarker(IAppDbContext db, IDateTime clock, MakeupCreditService credits, StudentPackageConsumer packages)
    {
        _db = db;
        _clock = clock;
        _credits = credits;
        _packages = packages;
    }

    /// <summary>İşaretlemeyi uygular; yeni verilen kredi sayısını döner.</summary>
    public async Task<int> MarkAsync(
        Guid studioId, ClassSession session, Guid markedByUserId, StudioRole? role,
        IReadOnlyList<AttendanceMarkInput> items, CancellationToken ct)
    {
        // Kredi vermeyi gerektiren bir kalem var mı? (yalnızca Excused/LateCancel + grant kredi verir.)
        var wantsGrant = items.Any(i => i.GrantMakeupCredit
            && (i.Status == AttendanceStatus.Excused || i.Status == AttendanceStatus.LateCancel));

        StudioMakeupPolicy? policy = null;
        if (wantsGrant)
        {
            policy = await _credits.GetPolicyAsync(studioId, ct);
            // Instructor, politika kapalıysa yoklama üzerinden kredi veremez.
            if (role == StudioRole.Instructor && !policy.AllowInstructorManualGrant)
                throw new ForbiddenException("Politika gereği hocalar yoklama üzerinden telafi kredisi veremez.");
        }
        var expiresAt = policy is null ? null : _credits.ComputeExpiry(policy);

        // Paket hakkı düşürme yalnızca Owner/StudioAdmin yetkisidir (finans). Instructor yapamaz.
        if (role == StudioRole.Instructor && items.Any(i => i.ConsumePackageCredit))
            throw new ForbiddenException("Paket hakkı düşürme yetkisi yalnızca Owner/StudioAdmin'dedir.");

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

            // Mazeretli/Geç iptal + grant + bu yoklamadan henüz kredi verilmediyse → +1 telafi kredisi.
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

            // Paket hakkı düşürme (staff seçimi). Aynı yoklama için tek kez (consumer idempotent). Uygun paket
            // yoksa/bakiye yetmezse consumer ConflictException fırlatır → tüm transaction rollback (yoklama da
            // kaydedilmez), staff hatayı görür ve paket seçer.
            if (item.ConsumePackageCredit)
            {
                var pkgReason = item.Status switch
                {
                    AttendanceStatus.Present => StudentPackageTransactionReason.AttendancePresent,
                    AttendanceStatus.Absent => StudentPackageTransactionReason.AttendanceAbsent,
                    AttendanceStatus.LateCancel => StudentPackageTransactionReason.AttendanceLateCancel,
                    _ => StudentPackageTransactionReason.Other,
                };
                await _packages.ConsumeForAttendanceAsync(
                    studioId, booking.StudentProfileId, att.Id, booking.Id, item.StudentPackageId,
                    markedByUserId, pkgReason, ct);
            }
        }

        return credited;
    }
}
