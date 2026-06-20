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

// Sprint 28B: borç artık LINE-bazlı (stored balance yok). Kalemler line read-model'inden (StudentContractLineDto);
// özet currency bazında; cross-currency tek toplam YOK. ContractAmendment debt'e KATILMAZ (28A backfill line.TotalPrice'a
// yedirdi). "Unassigned" = hiçbir line'a sayılmayan Paid ödemeler (line'sız + çok-line header contractId'li), bilgi amaçlı.

// ---------------------------------------------------------------------------------------------------
// Sprint 24 — Öğrenci self-servis (salt-okunur) trimmed DTO'ları. Staff-internal alanlar (CreatedByName,
// staff Notes, Price, audit snapshot) ÖĞRENCİYE SIZMAZ. Borç multi-currency-safe (tek cross-currency toplam yok).
// ---------------------------------------------------------------------------------------------------

/// <summary>Öğrenciye gösterilen sözleşme ek işlemi (trimmed) — kim yaptığı / audit snapshot YOK.</summary>
public sealed record StudentAmendmentDto(
    ContractAmendmentKind Kind,
    int AdditionalLessonCount,
    bool DatesExtended,
    DateOnly NewEndDate,
    decimal AdditionalPrice,
    string Currency,
    string? Description,
    DateTime CreatedAt);

/// <summary>Öğrenciye gösterilen sözleşme (trimmed) — Price / staff Notes / internal alan YOK. Ücret bilgisi borç bölümünde.</summary>
public sealed record StudentContractViewDto(
    Guid Id,
    string ClassTypeName,
    string InstructorName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalLessonCount,
    int ScheduledLessonCount,
    int AvailableLessonCount,
    StudentContractStatus Status,
    IReadOnlyList<StudentAmendmentDto> Amendments);

/// <summary>Para birimi bazlı borç bloğu (line toplamları) — cross-currency toplam ÜRETİLMEZ.</summary>
public sealed record CurrencyDebtDto(
    string Currency,
    decimal Total,
    decimal Paid,
    decimal Remaining);

/// <summary>Para birimi bazlı atanmamış (hiçbir line'a sayılmayan) ödeme bloğu (bilgi amaçlı).</summary>
public sealed record CurrencyPaidDto(string Currency, decimal Paid);

/// <summary>Borç özeti (Sprint 28B line-bazlı): currency özet + line kalemleri + atanmamış ödemeler. Staff + student ortak.</summary>
public sealed record LineDebtSummaryDto(
    IReadOnlyList<CurrencyDebtDto> ByCurrency,
    IReadOnlyList<StudentContractLineDto> Lines,
    IReadOnlyList<CurrencyPaidDto> UnassignedByCurrency);

// Staff + student aynı şekli kullanır (mevcut URL'ler korunur; tip adı geriye uyum için alias).
public sealed record StudentDebtSummaryDto(
    IReadOnlyList<CurrencyDebtDto> ByCurrency,
    IReadOnlyList<StudentContractLineDto> Lines,
    IReadOnlyList<CurrencyPaidDto> UnassignedByCurrency);

public sealed record MyDebtSummaryDto(
    IReadOnlyList<CurrencyDebtDto> ByCurrency,
    IReadOnlyList<StudentContractLineDto> Lines,
    IReadOnlyList<CurrencyPaidDto> UnassignedByCurrency);
