using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Telafi kredisi defter (ledger) hareketi. Tek bir tamsayı bakiye yerine her kredi olayı ayrı bir
/// kayıt olarak tutulur → kredi NEDEN verildi, KİM verdi, NE ZAMAN kullanıldı izlenebilir.
/// Bakiye = aktif (silinmemiş) hareketlerin <see cref="Amount"/> toplamı. Hareketler SOFT DELETE
/// edilmez; yanlış işlem için ters (Revoked/Restored) hareket eklenir. Tenant-scoped, StudentProfileId
/// üzerinden çalışır (UserId değil — misafir öğrenci de kredi taşıyabilir).
/// </summary>
public class MakeupCreditTransaction : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentProfileId { get; set; }
    public StudentProfile? StudentProfile { get; set; }

    /// <summary>Sprint 28A: kredinin doğduğu sözleşme SATIRI (line). "Telafi kredisi hangi line'dan doğduysa
    /// o line'a bağlı" kararı. 28A'da backfill (booking üzerinden) ile doldurulur; 28C'de YENİ krediler için
    /// ZORUNLU + balance line-scoped. NULL → eski/bağlanamamış (28A'da bağlanamayan dev kayıtları soft-delete).</summary>
    public Guid? StudentContractLineId { get; set; }
    public StudentContractLine? StudentContractLine { get; set; }

    /// <summary>İşaretli miktar: Granted/Restored pozitif, Used/Revoked/Expired negatif. 0 olamaz.</summary>
    public int Amount { get; set; }

    public MakeupCreditTransactionType Type { get; set; }
    public MakeupCreditReason Reason { get; set; }

    /// <summary>Krediyi doğuran seans (ör. iptal edilen ders). Aynı seans için tekrar verme engelinde kullanılır.</summary>
    public Guid? SourceClassSessionId { get; set; }

    /// <summary>Krediyi doğuran rezervasyon (ör. iptal edilen booking).</summary>
    public Guid? SourceBookingId { get; set; }

    /// <summary>Kredinin tüketildiği telafi rezervasyonu (Used hareketlerinde dolu).</summary>
    public Guid? UsedForBookingId { get; set; }

    /// <summary>Geçerlilik sonu (politika DefaultCreditValidityDays). Bu sürümde STORE edilir ama bakiyede ZORLANMAZ.</summary>
    public DateTime? ExpiresAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string? Notes { get; set; }
}
