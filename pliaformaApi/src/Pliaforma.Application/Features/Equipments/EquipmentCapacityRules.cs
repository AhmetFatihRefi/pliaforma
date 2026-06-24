using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>
/// Sprint 32B: Ekipman kapasitesi türetilmiş hesapları (stored counter YOK). Bir Equipment havuzunun
/// belirli bir <c>[start, end)</c> (yarı-açık) aralığındaki kullanımı = o havuza bağlı ders tiplerinin
/// çakışan Scheduled seanslarının <c>Capacity</c> toplamı. Sınırsız (9999) seanslar HARİÇ tutulur (§6:
/// ekipman öncesi legacy seanslar; ekipman-bağlı yeni/güncel seanslar sınırsız olamaz).
/// </summary>
public static class EquipmentCapacityRules
{
    /// <summary>Grup-sınırsız sentinel (ClassSession.Capacity). Ekipman-bağlı seansta YASAK (32B).</summary>
    public const int UnlimitedCapacity = 9999;

    public static bool IsUnlimited(int capacity) => capacity >= UnlimitedCapacity;

    /// <summary>Toplam kapasite (kişi) = Quantity * PerUnitConcurrentUsage.</summary>
    public static int TotalCapacity(int quantity, int perUnitConcurrentUsage) => quantity * perUnitConcurrentUsage;

    /// <summary>
    /// Sprint 32B/33: Otomatik program üretiminde ekipman invariant'ı — ekipman-bağlı ders tipinde üretilecek seans
    /// kapasitesi <b>sonlu</b> olmalı (sınırsız/9999 YASAK). Sprint 33: sonlu kapasite line'ın PreferredMaxCapacity'sinden
    /// ya da üretim anında seçilen kapasiteden gelir → ekipman-bağlı grup üretimi artık MÜMKÜN (yeter ki kapasite sonlu).
    /// Ekipmansız ders tipi etkilenmez. Çağıran ayrıca <see cref="EquipmentCapacityGuard"/> ile availability doğrular.
    /// </summary>
    public static void EnsureFiniteForEquipment(ClassType classType, int effectiveCapacity)
    {
        if (classType.EquipmentId is not null && effectiveCapacity >= UnlimitedCapacity)
            throw new ConflictException(
                "Ekipman bağlı derslerde sınırsız kapasiteyle program üretilemez. Sözleşmede kapasite tercihi belirleyin ya da program oluştururken sonlu seans kapasitesi seçin.");
    }

    /// <summary>
    /// <paramref name="equipmentId"/> havuzunu kullanan, <c>[start, end)</c> ile çakışan Scheduled seansların
    /// rezerve kapasite toplamı. Sınırsız (9999) seanslar dahil edilmez. <paramref name="excludeSessionIds"/>
    /// güncellenen/taşınan seans(lar)ı dışlar. Yarı-açık overlap: <c>s.StartAt &lt; end &amp;&amp; start &lt; s.EndAt</c>.
    /// </summary>
    public static async Task<int> UsedCapacityAsync(
        IAppDbContext db, Guid studioId, Guid equipmentId, DateTime start, DateTime end,
        IReadOnlyCollection<Guid> excludeSessionIds, CancellationToken ct)
    {
        var sum = await db.ClassSessions
            .Where(s => s.StudioId == studioId
                        && s.Status == ClassSessionStatus.Scheduled
                        && !excludeSessionIds.Contains(s.Id)
                        && s.Capacity < UnlimitedCapacity
                        && s.StartAt < end && start < s.EndAt
                        && db.ClassTypes.Any(c => c.Id == s.ClassTypeId && c.EquipmentId == equipmentId))
            .SumAsync(s => (int?)s.Capacity, ct);
        return sum ?? 0;
    }
}
