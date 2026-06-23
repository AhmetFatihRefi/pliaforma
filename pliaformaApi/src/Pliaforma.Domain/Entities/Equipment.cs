using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Sprint 32A: Stüdyonun fiziksel ekipman/envanter havuzu (reformer, mat, vb.).
/// Bir <see cref="ClassType"/> opsiyonel olarak bir Equipment havuzuna bağlanır (ClassType.EquipmentId, N:1).
/// Toplam kapasite = Quantity * PerUnitConcurrentUsage (kişi cinsinden).
/// Kapasite enforcement 32B'de gelir; bu sprintte yalnız veri + CRUD + bağ vardır (hiçbir mantık okumaz).
/// </summary>
public class Equipment : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>Stüdyoda bu ekipmandan kaç adet var (>= 0; 0 = cihaz yok/arızalı → toplam kapasite 0).</summary>
    public int Quantity { get; set; }

    /// <summary>Bir ekipmanı aynı anda kaç kişi kullanabilir (default 1, >= 1). Reformer/Mat için normalde 1.</summary>
    public int PerUnitConcurrentUsage { get; set; } = 1;

    /// <summary>Pasifse availability hesabına katılmaz (32B). Soft-delete yerine pasifleştirme önerilir.</summary>
    public bool IsActive { get; set; } = true;
}
