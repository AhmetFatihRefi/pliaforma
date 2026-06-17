namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Öğrenci paketinden ders hakkı tüketimini ATOMİK yönetir (race-safe). Somut uygulama (atomik koşullu
/// SQL UPDATE) Persistence'tadır; Application sağlayıcıya bağlanmaz. Aktif transaction içinde çağrılır.
/// </summary>
public interface IStudentPackageCreditService
{
    /// <summary>
    /// Paketten 1 ders hakkı tüketmeye çalışır: yalnızca paket Active ve CurrentCreditBalance &gt; 0 ise
    /// atomik olarak 1 azaltır; bakiye 0'a inerse Status'u UsedUp yapar. Tüketemezse false döner.
    /// Satır kilidi commit'e kadar tutulur → eşzamanlı tüketimlerde bakiye negatife düşmez.
    /// </summary>
    Task<bool> TryConsumeAsync(Guid studioId, Guid studentPackageId, CancellationToken ct = default);
}
