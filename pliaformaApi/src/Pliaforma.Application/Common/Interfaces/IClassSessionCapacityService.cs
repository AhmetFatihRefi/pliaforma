namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Ders seansı kapasite tüketimini ATOMİK yönetir (race-safe). Somut uygulama (atomik SQL UPDATE)
/// Persistence'tadır; Application sağlayıcıya bağlanmaz. Aktif transaction içinde çağrılır.
/// </summary>
public interface IClassSessionCapacityService
{
    /// <summary>
    /// Koltuk rezerve etmeye çalışır: BookedCount &lt; Capacity ise atomik olarak artırır.
    /// Doluysa false döner (artış yapılmaz). Satır kilidi commit'e kadar tutulur → eşzamanlı
    /// rezervasyonlarda yalnızca biri başarılı olur.
    /// </summary>
    Task<bool> TryReserveSeatAsync(Guid studioId, Guid classSessionId, CancellationToken ct = default);

    /// <summary>İptalde koltuğu serbest bırakır: BookedCount'u atomik azaltır (0'ın altına inmez).</summary>
    Task ReleaseSeatAsync(Guid studioId, Guid classSessionId, CancellationToken ct = default);
}
