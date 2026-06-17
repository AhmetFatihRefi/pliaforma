using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.ClassTypes;

public sealed record ClassTypeDto(
    Guid Id,
    string Name,
    EquipmentType Equipment,
    ClassCapacityType CapacityType,
    int MaxCapacity,
    ClassLevel Level,
    ClassPurpose Purpose,
    int DefaultDurationMinutes,
    string? Description,
    bool IsActive)
{
    public static ClassTypeDto From(ClassType c) =>
        new(c.Id, c.Name, c.Equipment, c.CapacityType, c.MaxCapacity, c.Level, c.Purpose,
            c.DefaultDurationMinutes, c.Description, c.IsActive);
}

/// <summary>Kapasite tipine göre MaxCapacity doğrulaması (Private⇒1, Duo⇒2, Group⇒2..50).</summary>
public static class CapacityRules
{
    public const int GroupMax = 50;

    public static bool IsValid(ClassCapacityType type, int capacity) => type switch
    {
        ClassCapacityType.Private => capacity == 1,
        ClassCapacityType.Duo => capacity == 2,
        ClassCapacityType.Group => capacity is >= 2 and <= GroupMax,
        _ => false,
    };

    public const string Message = "Kapasite tipiyle uyumsuz: Private⇒1, Duo⇒2, Group⇒2..50.";
}
