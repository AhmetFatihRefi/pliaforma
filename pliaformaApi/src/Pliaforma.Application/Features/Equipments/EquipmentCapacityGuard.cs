using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>
/// Sprint 32B: ClassSession create/update/bulk/reschedule sırasında ekipman kapasite guard'ı.
/// <see cref="ClassType.EquipmentId"/> null → guard YOK (eski davranış). Bağlıysa hard kontroller:
/// (1) sınırsız kapasite (9999) yasak, (2) ekipman aktif olmalı, (3) capacity ≤ TotalCapacity,
/// (4) capacity ≤ available (= total − çakışan kullanım). Eşzamanlılık: Equipment satırı FOR UPDATE ile
/// kilitlenir (aktif transaction commit'e kadar) → son kapasite iki kez verilmez. Booking yoluna DOKUNMAZ.
/// </summary>
public sealed class EquipmentCapacityGuard
{
    private readonly IAppDbContext _db;
    private readonly IEquipmentPoolLock _lock;

    public EquipmentCapacityGuard(IAppDbContext db, IEquipmentPoolLock poolLock)
    {
        _db = db;
        _lock = poolLock;
    }

    /// <summary>
    /// Ekipman-bağlı bir aday seansın geçerliliğini doğrular; ihlalde <see cref="ConflictException"/> (Türkçe,
    /// kullanıcı-dostu, GUID'siz) fırlatır. <paramref name="pendingOverlapCapacity"/>: aynı toplu işlemde henüz
    /// DB'ye yazılmamış, <c>[start,end)</c> ile çakışan seansların rezerve kapasitesi (bulk over-allocation guard);
    /// diğer akışlarda 0.
    /// </summary>
    public async Task EnsureCanReserveAsync(
        Guid studioId, ClassType classType, DateTime start, DateTime end, int capacity,
        Guid? excludeSessionId, int pendingOverlapCapacity, CancellationToken ct)
    {
        if (classType.EquipmentId is not { } equipmentId)
            return; // ekipman kısıtı yok → eski davranış

        // (1) Sınırsız kapasite ekipman-bağlı derste yasak (§6, hard).
        if (EquipmentCapacityRules.IsUnlimited(capacity))
            throw new ConflictException("Ekipman bağlı derslerde sınırsız kapasite kullanılamaz. Lütfen sonlu bir kapasite girin.");

        // Eşzamanlılık: havuzu kilitle (FOR UPDATE) → used yeniden hesaplanır, son kapasite tek kez verilir.
        await _lock.LockEquipmentAsync(studioId, equipmentId, ct);

        var equipment = await _db.Equipment
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.StudioId == studioId, ct)
            ?? throw new NotFoundException("Ekipman", equipmentId);

        // (2) Pasif ekipman → kullanılmaz.
        if (!equipment.IsActive)
            throw new ConflictException("Bu ders tipinin ekipmanı pasif; ders oluşturulamaz/güncellenemez. Önce ekipmanı etkinleştirin.");

        var total = EquipmentCapacityRules.TotalCapacity(equipment.Quantity, equipment.PerUnitConcurrentUsage);

        // (3) capacity > toplam → red (Quantity=0 → total 0 dahil).
        if (capacity > total)
            throw new ConflictException($"Bu ders için istenen kapasite ({capacity}) ekipman toplam kapasitesini ({total}) aşıyor.");

        // (4) capacity > available → red.
        var exclude = excludeSessionId is { } ex ? new[] { ex } : System.Array.Empty<Guid>();
        var used = await EquipmentCapacityRules.UsedCapacityAsync(_db, studioId, equipmentId, start, end, exclude, ct)
                   + pendingOverlapCapacity;
        var available = total - used;
        if (capacity > available)
            throw new ConflictException(
                $"Bu saat aralığında ekipman kapasitesi yetersiz. En fazla {(available < 0 ? 0 : available)} kişilik kapasite uygun (istenen {capacity}).");
    }
}
