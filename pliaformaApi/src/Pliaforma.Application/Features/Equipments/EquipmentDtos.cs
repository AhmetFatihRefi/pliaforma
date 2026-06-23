using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Features.Equipments;

/// <summary>
/// Ekipman havuzu özeti (liste/CRUD yanıtı). TotalCapacity = Quantity * PerUnitConcurrentUsage.
/// BoundClassTypeCount: bu havuza bağlı ders tipi sayısı (silme/Restrict ve UI için).
/// </summary>
public sealed record EquipmentDto(
    Guid Id,
    string Name,
    string? Description,
    int Quantity,
    int PerUnitConcurrentUsage,
    int TotalCapacity,
    bool IsActive,
    int BoundClassTypeCount)
{
    public static EquipmentDto From(Equipment e, int boundClassTypeCount) =>
        new(e.Id, e.Name, e.Description, e.Quantity, e.PerUnitConcurrentUsage,
            e.Quantity * e.PerUnitConcurrentUsage, e.IsActive, boundClassTypeCount);
}

/// <summary>Tek ekipman detayı + bağlı ders tipleri (UI'da "bu havuzu kullanan gruplar").</summary>
public sealed record EquipmentDetailDto(
    Guid Id,
    string Name,
    string? Description,
    int Quantity,
    int PerUnitConcurrentUsage,
    int TotalCapacity,
    bool IsActive,
    IReadOnlyList<BoundClassTypeDto> BoundClassTypes);

public sealed record BoundClassTypeDto(Guid Id, string Name);
