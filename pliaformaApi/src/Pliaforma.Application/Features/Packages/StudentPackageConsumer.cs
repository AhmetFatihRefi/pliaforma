using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Packages;

/// <summary>
/// Yoklamadan paket hakkı tüketiminin çekirdeği (AttendanceMarker tarafından çağrılır). Bir yoklama için
/// **en fazla bir** Used hareketi oluşturur (idempotent). Paket bakiyesinden ATOMİK düşer (race-safe);
/// kullanılabilir paket yoksa/bakiye yetmezse/paketin süresi dolmuşsa ConflictException fırlatır →
/// çağıranın transaction'ı geri alınır (yoklama da kaydedilmez), kullanıcı paket seçer/oluşturur.
/// SaveChanges ÇAĞIRMAZ.
/// Kullanılabilir paket: Status=Active, CurrentCreditBalance>0, ValidUntil null ya da bugün/ileri.
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
        Guid studioId, Guid studentProfileId, Guid attendanceId, Guid bookingId, Guid? studentPackageId,
        Guid byUserId, StudentPackageTransactionReason reason, CancellationToken ct)
    {
        // Aynı yoklama için ikinci kez düşme yok.
        var already = await _db.StudentPackageTransactions.AnyAsync(
            t => t.StudioId == studioId && t.SourceAttendanceId == attendanceId
                 && t.Type == StudentPackageTransactionType.Used, ct);
        if (already)
            return;

        // Stüdyo-yerel bugün (geçerlilik karşılaştırması için). ValidUntil değişmez → kilit gerekmez.
        var today = DateOnly.FromDateTime(await SessionRules.StudioNowAsync(_db, _clock, studioId, ct));

        StudentPackage pkg;
        if (studentPackageId is { } pid)
        {
            pkg = await _db.StudentPackages
                .FirstOrDefaultAsync(p => p.Id == pid && p.StudioId == studioId && p.StudentProfileId == studentProfileId, ct)
                ?? throw new NotFoundException("Öğrenci paketi", pid);
            if (pkg.Status != StudentPackageStatus.Active)
                throw new ConflictException("Seçili paket aktif değil.");
            if (pkg.ValidUntil is { } vu && vu < today)
                throw new ConflictException("Seçili paketin geçerlilik süresi dolmuş.");
        }
        else
        {
            // Otomatik seçim: aktif + bakiyesi olan + SÜRESİ DOLMAMIŞ, en yakın bitiş tarihli (süresizler en sonda).
            pkg = await _db.StudentPackages
                .Where(p => p.StudioId == studioId && p.StudentProfileId == studentProfileId
                            && p.Status == StudentPackageStatus.Active && p.CurrentCreditBalance > 0
                            && (p.ValidUntil == null || p.ValidUntil >= today))
                .OrderBy(p => p.ValidUntil == null).ThenBy(p => p.ValidUntil).ThenBy(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct)
                ?? throw new ConflictException("Öğrencinin kullanılabilir (süresi geçmemiş, bakiyesi olan) aktif paketi yok.");
        }

        // Atomik düşüm (bakiye>0 && Active ise). Yarış/boş bakiyede false → reddet.
        var ok = await _credit.TryConsumeAsync(studioId, pkg.Id, ct);
        if (!ok)
            throw new ConflictException("Paket bakiyesi yetersiz.");

        _db.StudentPackageTransactions.Add(new StudentPackageTransaction
        {
            StudioId = studioId,
            StudentPackageId = pkg.Id,
            StudentProfileId = studentProfileId,
            Amount = -1,
            Type = StudentPackageTransactionType.Used,
            Reason = reason,
            SourceAttendanceId = attendanceId,
            SourceClassBookingId = bookingId,
            CreatedByUserId = byUserId,
        });
    }
}
