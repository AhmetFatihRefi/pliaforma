using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Closures;

/// <summary>Kapalı gün / toplu işlem kaydı (sonuç özetiyle).</summary>
public sealed record StudioClosureDto(
    Guid Id,
    string Title,
    string? Reason,
    DateTime StartAt,
    DateTime EndAt,
    StudioClosureActionType ActionType,
    int? OffsetDays,
    Guid? AffectedInstructorProfileId,
    Guid? AffectedClassTypeId,
    int AffectedCount,
    int CancelledCount,
    int CreditedCount,
    int RescheduledCount,
    int SkippedCount,
    DateTime CreatedAt)
{
    public static StudioClosureDto From(StudioClosure c) =>
        new(c.Id, c.Title, c.Reason, c.StartAt, c.EndAt, c.ActionType, c.OffsetDays,
            c.AffectedInstructorProfileId, c.AffectedClassTypeId,
            c.AffectedCount, c.CancelledCount, c.CreditedCount, c.RescheduledCount, c.SkippedCount, c.CreatedAt);
}

/// <summary>Toplu işlemde tek bir seansın sonucu.</summary>
public sealed record ClosureSessionOutcomeDto(
    Guid SessionId,
    DateTime OriginalStartAt,
    DateTime? NewStartAt,
    string Outcome,
    string? Reason);

/// <summary>Closure oluşturma sonucu: kayıt + etkilenen seans dökümü.</summary>
public sealed record CreateClosureResultDto(
    StudioClosureDto Closure,
    IReadOnlyList<ClosureSessionOutcomeDto> Sessions);
