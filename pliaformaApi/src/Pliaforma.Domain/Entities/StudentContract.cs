using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Öğrenci–stüdyo sözleşmesi/anlaşması (Sprint 18a). Sözleşme-tabanlı program modelinin **yetki kaynağıdır**:
/// hangi ders tipinden, hangi hocayla, hangi tarih aralığında, kaç ders, kaç telafi, ne ücret. Programdan
/// program üretimi (GenerateContractSchedule) ve sözleşme-bağlı booking (ClassBooking.StudentContractId) bu
/// kayda dayanır. **StudentProfileId üzerinden çalışır** (misafir öğrenci dahil). Tenant-scoped.
///
/// NOT (Sprint 18a additive): paket/kredi modeli aynı anda eski akışta çalışmaya devam eder; sözleşme yolu
/// yalnızca StudentContractId verildiğinde devreye girer. Kesme (cutover) 18b'de.
/// </summary>
public class StudentContract : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentProfileId { get; set; }
    public StudentProfile? StudentProfile { get; set; }

    /// <summary>Anlaşılan ders grubu/tipi.</summary>
    public Guid ClassTypeId { get; set; }
    public ClassType? ClassType { get; set; }

    /// <summary>
    /// Anlaşılan (planlanan) varsayılan hoca (header düzeyi gruplama). Sprint 33: <c>null</c> olabilir — hocası
    /// belirtilmeden açılan line için otomatik üretilen header'da null kalır. Asıl yetki line üzerindedir.
    /// </summary>
    public Guid? InstructorProfileId { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }

    /// <summary>Sözleşme başlangıcı (stüdyo-yerel gün).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Sözleşme bitişi (dahil). Ödeme/hatırlatma bu tarihe göre.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Anlaşılan toplam ders hakkı (örn. 16). Telafi dersleri bundan DÜŞMEZ.</summary>
    public int TotalLessonCount { get; set; }

    /// <summary>Sözleşmeden gelen azami telafi hakkı (örn. 2). Faz 3'te kazanım/gating ile kullanılır.</summary>
    public int MakeupAllowance { get; set; }

    /// <summary>Anlaşılan ücret (elle girilir). Hoca hak edişiyle ilgisi YOKTUR.</summary>
    public decimal Price { get; set; }

    public string Currency { get; set; } = "TRY";

    /// <summary>Bu sözleşmeden üretilen dersler varsayılan olarak bireysel mi (grup mu).</summary>
    public bool IsIndividualDefault { get; set; }

    public StudentContractStatus Status { get; set; } = StudentContractStatus.Active;

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
}
