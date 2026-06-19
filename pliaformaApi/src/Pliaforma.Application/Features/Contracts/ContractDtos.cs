using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Contracts;

/// <summary>Öğrenci sözleşmesi özeti (+ türetilen ders sayıları).</summary>
public sealed record StudentContractDto(
    Guid Id,
    Guid StudentProfileId,
    string StudentName,
    Guid ClassTypeId,
    string ClassTypeName,
    Guid InstructorProfileId,
    string InstructorName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalLessonCount,
    int MakeupAllowance,
    decimal Price,
    string Currency,
    bool IsIndividualDefault,
    StudentContractStatus Status,
    string? Notes,
    int ScheduledLessonCount,
    int AvailableLessonCount);

/// <summary>Program üretimi için bir gün/saat slotu (haftanın günü + başlangıç saati).</summary>
public sealed record ContractScheduleSlotInput(DayOfWeek DayOfWeek, TimeOnly StartTime);

/// <summary>Program önizlemesinde tek bir ders adayı.</summary>
public sealed record ContractScheduleSlotPreviewDto(
    DateTime StartAt,
    DateTime EndAt,
    bool Plannable,
    string? SkipReason);

/// <summary>Program önizleme sonucu (oluşturma YAPILMAZ).</summary>
public sealed record ContractSchedulePreviewDto(
    int AvailableLessonCount,
    int PlannableCount,
    IReadOnlyList<ContractScheduleSlotPreviewDto> Slots);

/// <summary>Program üretim sonucu.</summary>
public sealed record GenerateContractScheduleResultDto(
    int CreatedCount,
    int NewSessionCount,
    int ReusedSessionCount,
    int SkippedCount,
    IReadOnlyList<DateTime> CreatedStartAts);

// ---------------------------------------------------------------------------------------------------
// Sprint 23 — Amendment (ek işlem) + borç DTO'ları.
// ---------------------------------------------------------------------------------------------------

/// <summary>Bir sözleşme ek işlemi (immutable kayıt).</summary>
public sealed record ContractAmendmentDto(
    Guid Id,
    Guid StudentContractId,
    ContractAmendmentKind Kind,
    int AdditionalLessonCount,
    int PreviousTotalLessonCount,
    int NewTotalLessonCount,
    DateOnly PreviousEndDate,
    DateOnly NewEndDate,
    decimal AdditionalPrice,
    string Currency,
    string? Description,
    string CreatedByName,
    DateTime CreatedAt);

/// <summary>Tek sözleşmenin borç dökümü (hesaplanır; stored balance yoktur).</summary>
public sealed record ContractDebtDto(
    Guid ContractId,
    string ClassTypeName,
    StudentContractStatus Status,
    string Currency,
    decimal BasePrice,
    decimal AdditionalCharges,
    decimal TotalCharges,
    decimal Paid,
    decimal Outstanding);

/// <summary>Öğrenci borç özeti — sözleşme bazlı kalemler + roll-up + etiketsiz ödemeler (bilgi amaçlı).</summary>
public sealed record StudentDebtSummaryDto(
    Guid StudentProfileId,
    string Currency,
    decimal TotalCharges,
    decimal TotalPaid,
    decimal TotalOutstanding,
    decimal UntaggedPaid,
    IReadOnlyList<ContractDebtDto> Contracts);
