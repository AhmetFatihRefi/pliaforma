using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Sprint 29A — Bir <see cref="StudentContractLine"/> üzerindeki öğrenci tercih gün/saati. Öğrencinin bu paket
/// kapsamında ders almak istediği gün (zorunlu) ve saat (opsiyonel) tercihleri. Bir line'da birden çok slot
/// olabilir (ör. Pazartesi 18:00 + Perşembe 19:00 + Cumartesi saatsiz). Aynı line içinde aynı
/// (DayOfWeek, PreferredStartTime) — NULL saat dahil — tekrar edemez.
///
/// Kullanım (29B): aday öğrenci seçiminde GÜN güçlü filtre, SAAT yakınlık sıralaması. 29A yalnız veri modeli +
/// CRUD + line kartı gösterimidir. Tenant-scoped.
/// </summary>
public class StudentContractLinePreferredSlot : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    /// <summary>Tercihin bağlı olduğu sözleşme satırı (line).</summary>
    public Guid StudentContractLineId { get; set; }
    public StudentContractLine? StudentContractLine { get; set; }

    /// <summary>Tercih edilen gün (zorunlu; System.DayOfWeek: Sunday=0 .. Saturday=6). DB'de string saklanır.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Tercih edilen başlangıç saati (opsiyonel; NULL → "saat farketmez").</summary>
    public TimeOnly? PreferredStartTime { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Line içindeki sıra (1..N).</summary>
    public int SequenceNo { get; set; } = 1;

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
}
