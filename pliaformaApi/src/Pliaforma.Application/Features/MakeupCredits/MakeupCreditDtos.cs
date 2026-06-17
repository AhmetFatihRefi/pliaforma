using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.MakeupCredits;

/// <summary>Tek bir telafi kredisi defter hareketi.</summary>
public sealed record MakeupCreditTransactionDto(
    Guid Id,
    int Amount,
    MakeupCreditTransactionType Type,
    MakeupCreditReason Reason,
    Guid? SourceClassSessionId,
    Guid? SourceBookingId,
    Guid? UsedForBookingId,
    DateTime? ExpiresAt,
    string? Notes,
    DateTime CreatedAt);

/// <summary>Bir öğrencinin kredi bakiyesi + hareket geçmişi (staff görünümü).</summary>
public sealed record StudentMakeupCreditsDto(
    Guid StudentProfileId,
    string StudentName,
    int Balance,
    IReadOnlyList<MakeupCreditTransactionDto> Transactions);

/// <summary>Öğrencinin kendi kredi bakiyesi + geçmişi.</summary>
public sealed record MyMakeupCreditsDto(
    int Balance,
    IReadOnlyList<MakeupCreditTransactionDto> Transactions);
