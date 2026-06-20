using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Students;

/// <summary>
/// Öğrenci profili DTO'su. HealthNotes hassastır: yalnızca yetkili görüntüleyiciye doldurulur
/// (From(..., includeHealth) ile), aksi halde null döner.
/// </summary>
public sealed record StudentDto(
    Guid Id,
    Guid? UserId,
    Guid? StudioMembershipId,
    string FullName,
    string? Phone,
    string? Email,
    StudentStatus Status,
    StudentSource Source,
    StudentLevel? Level,
    string? Notes,
    string? HealthNotes,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateOnly? BirthDate,
    Guid CreatedByUserId,
    DateTime? ConvertedAt,
    bool IsActive)
{
    public static StudentDto From(StudentProfile s, bool includeHealth) =>
        new(s.Id, s.UserId, s.StudioMembershipId, s.FullName, s.Phone, s.Email, s.Status, s.Source, s.Level,
            s.Notes, includeHealth ? s.HealthNotes : null, s.EmergencyContactName, s.EmergencyContactPhone,
            s.BirthDate, s.CreatedByUserId, s.ConvertedAt, s.IsActive);
}
