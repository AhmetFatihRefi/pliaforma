using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.MakeupCredits;

/// <summary>Tek bir telafi kredisi defter hareketi. Sprint 20: kaynak/kullanım etiketleri. 28C-2: line etiketi.</summary>
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
    string? UsedForLabel,    // kredinin kullanıldığı telafi dersi
    Guid? StudentContractLineId,
    string? LineLabel);      // Sprint 28C-2: hareketin bağlı olduğu line (ör. "Mat · 2026-06-01 – 2026-08-01")

/// <summary>Telafi özeti: kazanılan (toplam +), kullanılan/düşülen (toplam −), kullanılabilir (= bakiye).</summary>
public sealed record MakeupCreditSummaryDto(int Earned, int Used, int Available);

/// <summary>Sprint 28C-2: bir LINE'ın telafi kırılımı (kazanılan/kullanılan/kullanılabilir — pending rezerve düşülmüş).</summary>
public sealed record MakeupCreditByLineDto(
    Guid LineId,
    Guid ClassTypeId,
    string ClassTypeName,
    string LineLabel,
    int Earned,
    int Used,
    int Available,
    bool IsIndividual,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Bir öğrencinin kredi bakiyesi + özet + line kırılımı + hareket geçmişi (staff görünümü).</summary>
public sealed record StudentMakeupCreditsDto(
    Guid StudentProfileId,
    string StudentName,
    int Balance,
    MakeupCreditSummaryDto Summary,
    IReadOnlyList<MakeupCreditByLineDto> ByLine,
    IReadOnlyList<MakeupCreditTransactionDto> Transactions);

/// <summary>Öğrencinin kendi kredi bakiyesi + özet + line kırılımı + geçmişi (salt-okunur).</summary>
public sealed record MyMakeupCreditsDto(
    int Balance,
    MakeupCreditSummaryDto Summary,
    IReadOnlyList<MakeupCreditByLineDto> ByLine,
    IReadOnlyList<MakeupCreditTransactionDto> Transactions);

/// <summary>Telafi dersine eklenebilir (kullanılabilir telafi hakkı > 0) öğrenci. (Sprint 20 — dormant; 28C-2 line DTO'ya geçti.)</summary>
public sealed record MakeupEligibleStudentDto(Guid StudentProfileId, string StudentName, int AvailableCredits);

/// <summary>Sprint 28C-2: telafi dersine/talebine uygun LINE (kullanılabilir telafi hakkı > 0). Hem staff booking hem request eligible aynı modeli kullanır.</summary>
public sealed record MakeupEligibleLineDto(
    Guid LineId,
    Guid StudentProfileId,
    string StudentName,
    Guid ClassTypeId,
    string ClassTypeName,
    // Sprint 33: line hocası opsiyonel.
    Guid? InstructorProfileId,
    string? InstructorName,
    string LineLabel,
    int AvailableCredits,
    StudentLevel? Level,
    bool IsIndividual,
    DateOnly StartDate,
    DateOnly EndDate);
