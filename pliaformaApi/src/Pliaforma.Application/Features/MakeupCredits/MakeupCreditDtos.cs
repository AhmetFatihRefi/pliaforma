using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.MakeupCredits;

/// <summary>Tek bir telafi kredisi defter hareketi. Sprint 20: kaynak ders + kullanıldığı telafi dersi etiketleri.</summary>
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
    DateTime CreatedAt,
    string? SourceLabel,     // krediyi doğuran ders (ör. "Reformer · 2024-03-04 10:00")
    string? UsedForLabel);   // kredinin kullanıldığı telafi dersi

/// <summary>Telafi özeti: kazanılan (toplam +), kullanılan/düşülen (toplam −), kullanılabilir (= bakiye).</summary>
public sealed record MakeupCreditSummaryDto(int Earned, int Used, int Available);

/// <summary>Bir öğrencinin kredi bakiyesi + özet + hareket geçmişi (staff görünümü).</summary>
public sealed record StudentMakeupCreditsDto(
    Guid StudentProfileId,
    string StudentName,
    int Balance,
    MakeupCreditSummaryDto Summary,
    IReadOnlyList<MakeupCreditTransactionDto> Transactions);

/// <summary>Öğrencinin kendi kredi bakiyesi + özet + geçmişi (salt-okunur).</summary>
public sealed record MyMakeupCreditsDto(
    int Balance,
    MakeupCreditSummaryDto Summary,
    IReadOnlyList<MakeupCreditTransactionDto> Transactions);

/// <summary>Telafi dersine eklenebilir (kullanılabilir telafi hakkı > 0) öğrenci.</summary>
public sealed record MakeupEligibleStudentDto(Guid StudentProfileId, string StudentName, int AvailableCredits);
