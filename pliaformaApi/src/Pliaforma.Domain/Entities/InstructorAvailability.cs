using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir hocanın haftalık tekrar eden müsaitlik penceresi (stüdyo-yerel saat). Ders seansları bu
/// pencerelerin içine düşmek zorundadır (hoca için tanımlıysa). Tenant-scoped.
/// </summary>
public class InstructorAvailability : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid InstructorProfileId { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }

    /// <summary>Haftanın günü (System.DayOfWeek: Sunday=0 .. Saturday=6). DB'de string saklanır.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Stüdyo-yerel başlangıç saati (duvar saati).</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Stüdyo-yerel bitiş saati (StartTime'dan büyük olmalı).</summary>
    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;
}
