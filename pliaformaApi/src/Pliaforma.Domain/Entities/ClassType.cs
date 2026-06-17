using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// "Pilates grubu" — stüdyonun tanımladığı ders tipi. Dört eksen (ekipman × kapasite × seviye ×
/// amaç) + varsayılan süre ile modellenir. Öğrenci ders alacağı grubu (ClassType) seçer.
/// </summary>
public class ClassType : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public string Name { get; set; } = default!;

    public EquipmentType Equipment { get; set; }
    public ClassCapacityType CapacityType { get; set; }

    /// <summary>Azami kapasite. Private⇒1, Duo⇒2, Group⇒≥2 (validator doğrular).</summary>
    public int MaxCapacity { get; set; }

    public ClassLevel Level { get; set; }
    public ClassPurpose Purpose { get; set; }

    /// <summary>Varsayılan süre (dk). Sabit başlangıçlı seansta bitiş bundan hesaplanır.</summary>
    public int DefaultDurationMinutes { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InstructorClassType> Instructors { get; set; } = new List<InstructorClassType>();
}
