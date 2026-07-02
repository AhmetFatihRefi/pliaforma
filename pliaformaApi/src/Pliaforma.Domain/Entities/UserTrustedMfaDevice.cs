using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// "Bu cihazı hatırla" — MFA'yı belirli bir süre (varsayılan 30 gün) atlayan güvenilir cihaz kaydı (Sprint 50).
/// Cookie'deki ham opak token DB'de TUTULMAZ; yalnız SHA-256 hash'i (<see cref="TokenHash"/>) saklanır — DB
/// sızıntısında cookie düz elde edilemez. Süre dolunca (<see cref="ExpiresAt"/>) veya revoke edilince
/// (<see cref="RevokedAt"/>) geçersizdir; parola değişimi / MFA reset / yeniden kurulum bunları iptal eder.
/// </summary>
public class UserTrustedMfaDevice : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Cookie'deki ham token'ın SHA-256 hash'i (Base64). Ham değer yalnız HttpOnly cookie'de bulunur.</summary>
    public string TokenHash { get; set; } = default!;

    /// <summary>Güven süresinin sonu (fixed window; sliding DEĞİL). Dolduğunda MFA yeniden istenir.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Bu cihazla MFA'nın son atlandığı an (audit / ileride "cihazlarım" ekranı için).</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Revoke edildiği an (parola değişimi / MFA reset / yeniden kurulum). null → aktif.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Kaydı oluşturan istemci IP'si (opsiyonel; ileride revoke/cihaz listesi UI'ı için).</summary>
    public string? CreatedByIp { get; set; }
}
