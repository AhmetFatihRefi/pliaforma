using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Equipments;
using Pliaforma.Application.Features.Scheduling;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.ClassTypes;

/// <summary>
/// Sprint 32A: ClassType'a bağlanan ekipman havuzunun aktif stüdyoya ait olduğunu doğrular ve adını döner.
/// EquipmentId null → null döner (ekipman kısıtı yok). Başka stüdyonun/yokun id'si → NotFound.
/// </summary>
internal static class ClassTypeEquipmentResolver
{
    public static async Task<string?> ResolveNameAsync(
        IAppDbContext db, Guid studioId, Guid? equipmentId, CancellationToken ct)
    {
        if (equipmentId is not { } id)
            return null;

        var name = await db.Equipment
            .Where(e => e.Id == id && e.StudioId == studioId)
            .Select(e => e.Name)
            .FirstOrDefaultAsync(ct);

        return name ?? throw new NotFoundException("Ekipman", id);
    }

    /// <summary>
    /// Sprint 32B (§6 / madde 10 — A seçeneği): Bir ClassType'a ekipman bağlanırken (null→non-null veya farklı havuza
    /// geçiş) bu ders tipinin GELECEKTE planlanmış (Scheduled, StartAt ≥ stüdyo-şimdi) ve SINIRSIZ kapasiteli (9999)
    /// seansları varsa bağlamayı 409 ile reddeder. Kullanıcı önce o seanslara sonlu kapasite atamalıdır.
    /// </summary>
    public static async Task EnsureNoUnlimitedFutureSessionsAsync(
        IAppDbContext db, IDateTime clock, Guid studioId, Guid classTypeId, CancellationToken ct)
    {
        var nowLocal = await SessionRules.StudioNowAsync(db, clock, studioId, ct);
        var hasUnlimitedFuture = await db.ClassSessions.AnyAsync(s =>
            s.StudioId == studioId
            && s.ClassTypeId == classTypeId
            && s.Status == ClassSessionStatus.Scheduled
            && s.StartAt >= nowLocal
            && s.Capacity >= EquipmentCapacityRules.UnlimitedCapacity, ct);

        if (hasUnlimitedFuture)
            throw new ConflictException(
                "Bu ders tipine ekipman bağlanamaz. Gelecekte planlanmış sınırsız kapasiteli dersler var. Önce bu derslere sonlu kapasite atayın.");
    }
}
