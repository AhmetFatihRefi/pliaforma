using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Sözleşme ek işlemi / ek protokol (Sprint 23). Mevcut bir <see cref="StudentContract"/>'a ek ders hakkı,
/// bitiş tarihi uzatma ve/veya ek ücret ekler. **Append-only ledger'dır**: oluşturulduktan sonra
/// güncellenmez/silinmez; düzeltme yalnız ters işaretli yeni bir amendment ile yapılır (Kind=Correction).
///
/// Para tarafı: sözleşmenin <c>Price</c>'ı ORİJİNAL kalır; ek ücretler bu kayıtların <see cref="AdditionalPrice"/>
/// toplamıdır. Borç = (Price + Σ AdditionalPrice) − Σ (sözleşmeye bağlı Paid ödemeler). Stored balance YOK.
///
/// Ders hakkı/tarih tarafı: amendment uygulanırken contract.TotalLessonCount ve contract.EndDate canlı olarak
/// güncellenir (AvailableLessonCount ve program üretimi bunları okur); bu kayıt Previous/New snapshot'ı audit
/// için tutar. Tenant-scoped; yalnız Owner/StudioAdmin oluşturur.
/// </summary>
public class ContractAmendment : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentContractId { get; set; }
    public StudentContract? StudentContract { get; set; }

    /// <summary>Raporlama/filtreleme kategorisi (yardımcı). Doğruluk delta alanlarındadır.</summary>
    public ContractAmendmentKind Kind { get; set; }

    /// <summary>Ek ders hakkı deltası (negatif olabilir = düzeltme/azaltma).</summary>
    public int AdditionalLessonCount { get; set; }

    /// <summary>İşlemden önceki toplam ders hakkı (audit snapshot).</summary>
    public int PreviousTotalLessonCount { get; set; }

    /// <summary>İşlemden sonraki toplam ders hakkı (audit snapshot).</summary>
    public int NewTotalLessonCount { get; set; }

    /// <summary>İşlemden önceki bitiş tarihi (audit snapshot).</summary>
    public DateOnly PreviousEndDate { get; set; }

    /// <summary>İşlemden sonraki bitiş tarihi (yalnız ileri alınabilir; değişmezse Previous'a eşit).</summary>
    public DateOnly NewEndDate { get; set; }

    /// <summary>Ek ücret deltası (negatif olabilir = indirim/düzeltme).</summary>
    public decimal AdditionalPrice { get; set; }

    public string Currency { get; set; } = "TRY";

    public string? Description { get; set; }

    public Guid CreatedByUserId { get; set; }
}
