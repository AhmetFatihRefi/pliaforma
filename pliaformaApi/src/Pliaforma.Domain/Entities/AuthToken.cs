using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Tek-kullanımlık auth token (Sprint 16): e-posta doğrulama + şifre sıfırlama. **Ham token DB'de
/// TUTULMAZ** — yalnız SHA-256 hash'i (<see cref="TokenHash"/>) saklanır; ham değer kullanıcıya giden
/// linke konur. Tek kullanımlık (<see cref="ConsumedAt"/>) + süreli (<see cref="ExpiresAt"/>); yeni token
/// üretilince aynı purpose'un eski kullanılmamışları soft-delete ile iptal edilir.
/// </summary>
public class AuthToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public AuthTokenPurpose Purpose { get; set; }

    /// <summary>Ham token'ın SHA-256 hash'i (Base64). Ham değer yalnızca kullanıcıya/maile gider.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Tüketildiği an (UTC). Null → henüz kullanılmadı.</summary>
    public DateTime? ConsumedAt { get; set; }

    public string? CreatedIp { get; set; }
    public string? UserAgent { get; set; }
}
