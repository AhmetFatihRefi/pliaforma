using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
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
}
