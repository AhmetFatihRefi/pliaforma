using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir öğrencinin bir ders seansına rezervasyonu. **StudentProfileId üzerinden çalışır** (UserId değil)
/// — misafir öğrenci de (staff aracılığıyla) rezerve edilebilir. Tenant-scoped. İptalde silinmez,
/// Status değişir; aktif rezervasyon yalnızca Status=Booked'dır.
/// </summary>
public class ClassBooking : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid ClassSessionId { get; set; }
    public ClassSession? ClassSession { get; set; }

    public Guid StudentProfileId { get; set; }
    public StudentProfile? StudentProfile { get; set; }

    /// <summary>
    /// Bu rezervasyonun çekildiği öğrenci sözleşmesi (Sprint 18a — sözleşme-tabanlı program). Dolu →
    /// sözleşme yolu (paket uygunluğu yerine sözleşme available-lesson kontrolü). null → eski paket/booking
    /// yolu (geri uyum; 18b cutover'a kadar korunur). FK SetNull (sözleşme silinse de booking izi kalır).
    /// </summary>
    public Guid? StudentContractId { get; set; }
    public StudentContract? StudentContract { get; set; }

    /// <summary>Sprint 28A: rezervasyonun çekildiği sözleşme SATIRI (line). 28A'da backfill ile doldurulur;
    /// line-bazlı available/telafi (28C) buna dayanacak. NULL → henüz line'a bağlanmamış (eski/geçiş).</summary>
    public Guid? StudentContractLineId { get; set; }
    public StudentContractLine? StudentContractLine { get; set; }

    public ClassBookingStatus Status { get; set; } = ClassBookingStatus.Booked;

    /// <summary>Rezervasyonu yapan kullanıcı (öğrencinin kendisi veya vekaleten personel).</summary>
    public Guid BookedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Rezervasyonun kullanmayı planladığı <see cref="StudentPackage"/> (Sprint 14). Self/staff booking
    /// uygun paket bulduğunda yazılır; yoklama tüketiminde **öncelikli** kullanılır. Plain referans
    /// (FK yok — paket hard-delete edilmez, soft-delete edilir). null → bağlı paket yok.
    /// </summary>
    public Guid? ExpectedStudentPackageId { get; set; }

    /// <summary>
    /// true → bu rezervasyon paketsiz/yetersiz hakla (yalnız Owner/StudioAdmin) yapıldı; ödeme gerekiyor.
    /// Sonradan geçerli paketle yoklama tüketimi başarılı olursa false'a çekilir (PackageEligibilityStatus=Resolved).
    /// </summary>
    public bool RequiresPayment { get; set; }

    /// <summary>Rezervasyon anındaki paket uygunluk değerlendirmesi (görünürlük/raporlama için saklanır).</summary>
    public PackageEligibilityStatus? PackageEligibilityStatus { get; set; }
}
