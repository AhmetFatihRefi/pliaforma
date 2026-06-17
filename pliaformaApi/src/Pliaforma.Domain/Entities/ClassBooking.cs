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

    public ClassBookingStatus Status { get; set; } = ClassBookingStatus.Booked;

    /// <summary>Rezervasyonu yapan kullanıcı (öğrencinin kendisi veya vekaleten personel).</summary>
    public Guid BookedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
