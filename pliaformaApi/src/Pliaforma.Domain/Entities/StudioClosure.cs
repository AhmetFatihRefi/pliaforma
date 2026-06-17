using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Stüdyonun kapalı gün / toplu seans işlemi kaydı (tatil, toplu iptal/erteleme). Verilen tarih
/// aralığındaki planlı seanslara <see cref="ActionType"/> uygulanır. Bu RECURRING bir sistem DEĞİLDİR;
/// yalnızca o anda mevcut seanslar üzerinde tek seferlik toplu operasyondur. Sonuç sayıları izleme için
/// saklanır. Tenant-scoped. StartAt/EndAt stüdyo-yerel duvar saatidir (seans saatleriyle aynı eksen).
/// </summary>
public class StudioClosure : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public string Title { get; set; } = default!;
    public string? Reason { get; set; }

    /// <summary>Kapanış aralığı başlangıcı (stüdyo-yerel duvar saati).</summary>
    public DateTime StartAt { get; set; }

    /// <summary>Kapanış aralığı bitişi (stüdyo-yerel duvar saati).</summary>
    public DateTime EndAt { get; set; }

    public StudioClosureActionType ActionType { get; set; }

    /// <summary>RescheduleByOffset için gün ofseti (pozitif → ileri al).</summary>
    public int? OffsetDays { get; set; }

    /// <summary>Yalnızca bu hocanın seanslarını etkile (null → tümü).</summary>
    public Guid? AffectedInstructorProfileId { get; set; }

    /// <summary>Yalnızca bu ders tipinin seanslarını etkile (null → tümü).</summary>
    public Guid? AffectedClassTypeId { get; set; }

    // ---- Sonuç özeti (uygulandığı andaki sayımlar) ----
    public int AffectedCount { get; set; }
    public int CancelledCount { get; set; }
    public int CreditedCount { get; set; }
    public int RescheduledCount { get; set; }
    public int SkippedCount { get; set; }

    public Guid CreatedByUserId { get; set; }
}
