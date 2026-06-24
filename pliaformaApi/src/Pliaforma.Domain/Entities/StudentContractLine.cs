using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Öğrenci sözleşme satırı (Sprint 28A — line-tabanlı model). <see cref="StudentContract"/> artık üst başlıktır
/// (header); asıl hak/ücret/telafi/son-ödeme/bireysellik bu LINE üzerinde takip edilir. Bir öğrenci sözleşmesinde
/// birden çok line olabilir (ör. Mat Pilates line + Reformer line); her line kendi tarih aralığı, ders sayısı,
/// ders başı ücreti, telafi hakkı ve bireysellik bayrağıyla bağımsız izlenir. Yeni ders alımı eski line'ı
/// şişirmez → yeni line açılır.
///
/// "Ödenen/Kalan" STORED DEĞİLDİR: ödenen = bu line'a bağlı (Status=Paid) Payment toplamı; kalan = TotalPrice −
/// ödenen (mevcut borç hesabı deseninin line karşılığı). <see cref="DueDate"/> nullable — yalnız dolu olduğunda
/// 28D ödeme hatırlatması üretir. Tenant-scoped. (28A: model + backfill + read; line-bazlı yazma/scheduling/makeup
/// 28B/28C.)
/// </summary>
public class StudentContractLine : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    /// <summary>Üst başlık (header) sözleşme.</summary>
    public Guid StudentContractId { get; set; }
    public StudentContract? StudentContract { get; set; }

    /// <summary>Bu line'ın ders grubu/tipi.</summary>
    public Guid ClassTypeId { get; set; }
    public ClassType? ClassType { get; set; }

    /// <summary>
    /// Sprint 33: Bu line'ın TERCİH edilen hocası. <c>null</c> = "herhangi bir hoca" (hoca kuralı yok). Dolu ise HARD:
    /// aday eşleştirme/booking yalnız bu hocayı kabul eder; program üretiminde de bu hoca kullanılır. null ise üretim
    /// sırasında hoca seçtirilir (line'a kalıcı yazılmaz).
    /// </summary>
    public Guid? InstructorProfileId { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }

    /// <summary>
    /// Sprint 33: Kapasite tercihi. <c>null</c> = Farketmez (kural yok) · <c>1</c> = Bireysel · <c>N</c> = en fazla N kişilik.
    /// Kaynak alan budur; <see cref="IsIndividual"/> bundan TÜRETİLİR (== 1 ise true). HARD: tercih varsa öğrenci yalnız
    /// kapasitesi ≤ tercih olan derslere eklenebilir; üretilen seans kapasitesi de buna göre belirlenir (1..50).
    /// </summary>
    public int? PreferredMaxCapacity { get; set; }

    /// <summary>Line başlangıcı (stüdyo-yerel gün). Normal dersler bu aralıkta üretilir.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Line bitişi (dahil). Normal dersler bu aralıkta; TELAFİ dersleri bu tarihten sonra da olabilir (28C).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Anlaşılan ders sayısı (telafi dersleri bundan DÜŞMEZ).</summary>
    public int LessonCount { get; set; }

    /// <summary>Ders başı ücret (otoriter). TotalPrice = LessonCount × PricePerLesson.</summary>
    public decimal PricePerLesson { get; set; }

    /// <summary>Toplam ücret (= LessonCount × PricePerLesson; sistemden hesaplanır).</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Bu line'ın telafi hakkı (line bazlı; 28C'de makeup line-scoped).</summary>
    public int MakeupAllowance { get; set; }

    /// <summary>Son ödeme tarihi (opsiyonel). Backfill'de NULL; yeni line ekranında girilir; 28D reminder yalnız dolu olanlarda.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Bireysel mi? Sprint 33: artık <see cref="PreferredMaxCapacity"/>'den TÜRETİLİR (== 1 ise true); doğrudan yazılmaz.</summary>
    public bool IsIndividual { get; set; }

    public StudentContractLineStatus Status { get; set; } = StudentContractLineStatus.Active;

    public string Currency { get; set; } = "TRY";

    /// <summary>Header içindeki sıra (1..N). Backfill'de 1.</summary>
    public int SequenceNo { get; set; } = 1;

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
}
