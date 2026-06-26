using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Sprint 38 — Bir sözleşme satırının (line) ödeme planındaki TEK taksit/vade. Peşin = tek satır (Amount = line.TotalPrice).
/// Taksitli = 2+ satır. Plan yoksa satır yok (eski davranış). Durum (Paid/Partial/Overdue/Planned) DB'de TUTULMAZ —
/// okuma anında line ödemesi en eski vadeden dağıtılarak hesaplanır (Payment installment'a bağlanmaz). Currency line'dan.
/// Tenant-scoped. Tekillik/sıra (SequenceNo) uygulama katmanında (full-replace + soft-delete).
/// </summary>
public class ContractLinePaymentInstallment : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentContractLineId { get; set; }
    public StudentContractLine? StudentContractLine { get; set; }

    /// <summary>Plan içi sıra (1..N). Backend'de DueDate artan sıraya göre atanır.</summary>
    public int SequenceNo { get; set; } = 1;

    public DateOnly DueDate { get; set; }

    /// <summary>Beklenen taksit tutarı (numeric(12,2); &gt; 0). Σ Amount = line.TotalPrice olmalı.</summary>
    public decimal Amount { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }
}
