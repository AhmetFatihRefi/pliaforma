using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir hocanın BELİRLİ bir stüdyodaki iş profili. Ücret stüdyo bazındadır (aynı hoca farklı
/// stüdyoda farklı ücret). Hocanın Instructor üyeliğiyle (StudioId+UserId) eşleşir.
/// </summary>
public class InstructorProfile : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string DisplayName { get; set; } = default!;
    public string? Bio { get; set; }

    /// <summary>Hocanın bu stüdyodaki ders ücreti.</summary>
    public decimal Rate { get; set; }
    public RateType RateType { get; set; } = RateType.PerSession;

    /// <summary>Takvim/UI rengi (opsiyonel).</summary>
    public string? Color { get; set; }

    public bool IsAcceptingStudents { get; set; } = true;

    public ICollection<InstructorClassType> ClassTypes { get; set; } = new List<InstructorClassType>();
}
