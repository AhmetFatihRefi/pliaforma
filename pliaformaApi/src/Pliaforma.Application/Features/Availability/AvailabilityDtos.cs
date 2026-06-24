namespace Pliaforma.Application.Features.Availability;

/// <summary>Sprint 32C: Boşluk bulma motoru çıktısı (window-based — slot-slot değil).</summary>
public sealed record AvailabilityInstructorDto(Guid Id, string Name);

/// <summary>
/// Bir "başlangıç penceresi": seçilen süreyle dersin başlayabileceği en erken (<see cref="StartWindowFrom"/>)
/// ve en geç (<see cref="StartWindowTo"/>) saat. Pencere boyunca <see cref="AvailableEquipmentCapacity"/>
/// (garanti edilen minimum kapasite; ekipman kısıtı yoksa null) ve <see cref="AvailableInstructors"/> SABİTtir
/// (değişirse pencere bölünür). 14:00–15:00 / 60dk → ders 14:00..15:00 arası herhangi bir başlangıçta açılabilir.
/// </summary>
public sealed record AvailabilityWindowDto(
    DateOnly Date,
    DateTime StartWindowFrom,
    DateTime StartWindowTo,
    int DurationMinutes,
    int RequiredCapacity,
    int? AvailableEquipmentCapacity,
    IReadOnlyList<AvailabilityInstructorDto> AvailableInstructors);
