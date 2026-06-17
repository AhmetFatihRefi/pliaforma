using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Features.Scheduling;

public sealed record InstructorAvailabilityDto(
    Guid Id,
    Guid InstructorProfileId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive)
{
    public static InstructorAvailabilityDto From(InstructorAvailability a) =>
        new(a.Id, a.InstructorProfileId, a.DayOfWeek, a.StartTime, a.EndTime, a.IsActive);
}
