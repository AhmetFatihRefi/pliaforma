namespace Pliaforma.Application.Features.Instructors;

/// <summary>Bir hocanın atandığı pilates grubu (ClassType) + opsiyonel ücret override.</summary>
public sealed record InstructorAssignmentDto(
    Guid Id,
    Guid ClassTypeId,
    string ClassTypeName,
    decimal? RateOverride);
