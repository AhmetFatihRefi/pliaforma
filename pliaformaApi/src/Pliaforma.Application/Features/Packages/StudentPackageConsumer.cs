using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Packages;

/// <summary>
/// Yoklamadan paket hakkı tüketiminin çekirdeği (AttendanceMarker tarafından çağrılır). Bir yoklama için
/// **en fazla bir** Used hareketi oluşturur (idempotent). Paket bakiyesinden ATOMİK düşer (race-safe).
/// SaveChanges ÇAĞIRMAZ — çağıranın transaction'ında.
/// <para>
/// <b>Paket seçimi (Sprint 14):</b> açık <c>explicitPackageId</c> &gt; booking'in
/// <see cref="ClassBooking.ExpectedStudentPackageId"/>'si &gt; otomatik seçim. Seçilen paket seansın ders
/// grubu (ClassType) için geçerli olmalı; değilse 409. Beklenen paket artık geçersiz/hakkı bittiyse
/// **sessiz fallback yapılmaz** → 409 (staff bilinçli paket seçsin). Tüketim başarılıysa booking'in
/// ExpectedStudentPackageId'si kullanılan pakete bağlanır; booking RequiresPayment ise temizlenir (Resolved).
/// </para>
/// </summary>
public sealed class StudentPackageConsumer
{
    private readonly IAppDbContext _db;
    private readonly IStudentPackageCreditService _credit;
    private readonly IDateTime _clock;

    public StudentPackageConsumer(IAppDbContext db, IStudentPackageCreditService credit, IDateTime clock)
    {
        _db = db;
        _credit = credit;
        _clock = clock;
    }

    public async Task ConsumeForAttendanceAsync(
        Guid studioId, ClassBooking booking, Guid classTypeId, Guid attendanceId,
        Guid? explicitPackageId, Guid byUserId, StudentPackageTransactionReason reason, CancellationToken ct)
    {
        var studentProfileId = booking.StudentProfileId;

        // Aynı yoklama için ikinci kez düşme yok.
        var already = await _db.StudentPackageTransactions.AnyAsync(
            t => t.StudioId == studioId && t.SourceAttendanceId == attendanceId
                 && t.Type == StudentPackageTransactionType.Used, ct);
        if (already)
            return;

        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));

        // Hedef: açık seçim > beklenen paket > otomatik.
        var targetId = explicitPackageId ?? booking.ExpectedStudentPackageId;
        var fromExpected = explicitPackageId is null && booking.ExpectedStudentPackageId is not null;

        StudentPackage pkg;
        if (targetId is { } pid)
        {
            pkg = await _db.StudentPackages
                .FirstOrDefaultAsync(p => p.Id == pid && p.StudioId == studioId && p.StudentProfileId == studentProfileId, ct)
                ?? throw new NotFoundException("Öğrenci paketi", pid);

            if (pkg.Status != StudentPackageStatus.Active || (pkg.ValidUntil is { } vu && vu < today))
                throw new ConflictException(fromExpected
                    ? "Beklenen paket artık geçerli değil veya hakkı kalmamış; lütfen geçerli bir paket seçin."
                    : pkg.Status != StudentPackageStatus.Active ? "Seçili paket aktif değil." : "Seçili paketin geçerlilik süresi dolmuş.");

            if (!await CoversClassTypeAsync(studioId, pkg, classTypeId, ct))
                throw new ConflictException("Seçili paket bu ders grubu için geçerli değil.");
        }
        else
        {
            // Otomatik seçim: aktif + bakiyesi olan + süresi dolmamış, bu ders grubu için geçerli olan,
            // en yakın bitiş tarihli (süresizler en sonda).
            var candidates = await _db.StudentPackages
                .Where(p => p.StudioId == studioId && p.StudentProfileId == studentProfileId
                            && p.Status == StudentPackageStatus.Active && p.CurrentCreditBalance > 0
                            && (p.ValidUntil == null || p.ValidUntil >= today))
                .OrderBy(p => p.ValidUntil == null).ThenBy(p => p.ValidUntil).ThenBy(p => p.CreatedAt)
                .ToListAsync(ct);

            StudentPackage? chosen = null;
            foreach (var c in candidates)
            {
                if (await CoversClassTypeAsync(studioId, c, classTypeId, ct)) { chosen = c; break; }
            }
            pkg = chosen ?? throw new ConflictException(
                "Öğrencinin bu ders grubu için kullanılabilir (süresi geçmemiş, bakiyesi olan) aktif paketi yok.");
        }

        // Atomik düşüm (bakiye>0 && Active ise). Yarış/boş bakiyede false → reddet.
        var ok = await _credit.TryConsumeAsync(studioId, pkg.Id, ct);
        if (!ok)
            throw new ConflictException(fromExpected
                ? "Beklenen paket artık geçerli değil veya hakkı kalmamış; lütfen geçerli bir paket seçin."
                : "Paket bakiyesi yetersiz.");

        _db.StudentPackageTransactions.Add(new StudentPackageTransaction
        {
            StudioId = studioId,
            StudentPackageId = pkg.Id,
            StudentProfileId = studentProfileId,
            Amount = -1,
            Type = StudentPackageTransactionType.Used,
            Reason = reason,
            SourceAttendanceId = attendanceId,
            SourceClassBookingId = booking.Id,
            CreatedByUserId = byUserId,
        });

        // Booking'i fiilen kullanılan pakete bağla; ödeme gerekiyorduysa çözüldü olarak işaretle.
        booking.ExpectedStudentPackageId = pkg.Id;
        if (booking.RequiresPayment)
        {
            booking.RequiresPayment = false;
            booking.PackageEligibilityStatus = PackageEligibilityStatus.Resolved;
        }
    }

    /// <summary>Paket (snapshot'lanmış kapsamına göre) verilen ders grubunu kapsıyor mu?</summary>
    private async Task<bool> CoversClassTypeAsync(Guid studioId, StudentPackage pkg, Guid classTypeId, CancellationToken ct) =>
        pkg.AllowAllClassTypesSnapshot
        || await _db.StudentPackageAllowedClassTypes.AnyAsync(
               a => a.StudioId == studioId && a.StudentPackageId == pkg.Id && a.ClassTypeId == classTypeId, ct);
}
