using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Scheduling;

public sealed record ClassSessionDto(
    Guid Id,
    Guid InstructorProfileId,
    string InstructorName,
    Guid ClassTypeId,
    string ClassTypeName,
    DateTime StartAt,
    DateTime EndAt,
    int Capacity,
    int BookedCount,
    ClassSessionStatus Status,
    ClassSessionKind SessionKind,
    string? Notes,
    Guid CreatedByUserId,
    DateTime? RescheduledAt,
    bool IsIndividual = false,
    Guid? ActualInstructorProfileId = null)
{
    public int RemainingCapacity => Math.Max(Capacity - BookedCount, 0);
    public bool IsFull => BookedCount >= Capacity;
    /// <summary>Sprint 18a: planlanan ≠ fiilî hoca (hoca değişikliği). Mantık Faz 2.</summary>
    public bool InstructorChanged => ActualInstructorProfileId is not null && ActualInstructorProfileId != InstructorProfileId;

    public static ClassSessionDto From(ClassSession s, string instructorName, string classTypeName) =>
        new(s.Id, s.InstructorProfileId, instructorName, s.ClassTypeId, classTypeName, s.StartAt, s.EndAt,
            s.Capacity, s.BookedCount, s.Status, s.SessionKind, s.Notes, s.CreatedByUserId, s.RescheduledAt,
            s.IsIndividual, s.ActualInstructorProfileId);
}

/// <summary>Aralık içi slot üretiminin sonucu: oluşturulanlar + atlananlar (neden ile).</summary>
public sealed record BulkGenerateResultDto(
    IReadOnlyList<ClassSessionDto> Created,
    IReadOnlyList<SkippedSlotDto> Skipped);

public sealed record SkippedSlotDto(DateTime StartAt, string Reason);
