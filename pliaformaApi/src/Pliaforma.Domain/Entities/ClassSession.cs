using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Somut, takvimdeki bir ders seansı (bir hoca + bir pilates grubu, belirli bir zaman aralığında).
/// Sprint 4 rezervasyon sisteminin bağlanacağı birim. Bu sprintte öğrenci rezervasyonu YOKTUR;
/// Capacity yalnızca ileride kullanılmak üzere tutulur. Tenant-scoped.
/// StartAt/EndAt stüdyo-yerel duvar saatidir (timestamp without time zone).
/// </summary>
public class ClassSession : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid InstructorProfileId { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }

    public Guid ClassTypeId { get; set; }
    public ClassType? ClassType { get; set; }

    public DateTime StartAt { get; set; }

    /// <summary>StartAt + ClassType.DefaultDurationMinutes ile otomatik hesaplanır.</summary>
    public DateTime EndAt { get; set; }

    /// <summary>Rezervasyon kapasitesi. Verilmezse ClassType.MaxCapacity'den alınır; en az 1.</summary>
    public int Capacity { get; set; }

    /// <summary>Aktif (Booked) rezervasyon sayısı. Kapasite tüketimi atomik UPDATE ile yönetilir.</summary>
    public int BookedCount { get; set; }

    public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Scheduled;

    /// <summary>Seans türü (Regular/Makeup/Trial/Special). Makeup seansı rezervasyonu kredi tüketir.</summary>
    public ClassSessionKind SessionKind { get; set; } = ClassSessionKind.Regular;

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }

    // ---- Erteleme izi (Sprint 5) ----
    /// <summary>Seans en son ne zaman ertelendi (UTC). null → hiç ertelenmedi.</summary>
    public DateTime? RescheduledAt { get; set; }

    /// <summary>Son erteleme gerekçesi.</summary>
    public string? RescheduleReason { get; set; }
}
