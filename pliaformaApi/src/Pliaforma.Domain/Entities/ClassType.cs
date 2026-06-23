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

    /// <summary>
    /// Sprint 32A: opsiyonel ekipman havuzu bağı (N:1, <see cref="Equipment"/>). null → ekipman kısıtı YOK
    /// (eski davranış aynen sürer). Bir havuz birden çok ClassType tarafından paylaşılabilir.
    /// Navigation eklenmez (mevcut dormant <c>Equipment</c> enum'u ile ad çakışmasını önlemek için yalnız FK).
    /// </summary>
    public Guid? EquipmentId { get; set; }

    public ICollection<InstructorClassType> Instructors { get; set; } = new List<InstructorClassType>();
}
