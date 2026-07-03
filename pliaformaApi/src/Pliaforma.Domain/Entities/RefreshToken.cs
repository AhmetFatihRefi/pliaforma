using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Refresh token kaydı. Token'ın kendisi DB'de DÜZ METİN tutulmaz; yalnızca SHA-256
/// hash'i (TokenHash) saklanır. Rotasyonludur: yenilenince eskisi revoke edilir.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Ham token'ın SHA-256 hash'i (Base64). Ham değer yalnızca istemciye döner.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Bu token rotasyonla yenilendiğinde, yerine geçen token'ın hash'i.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    /// <summary>
    /// Sprint 53 — token'ın verildiği kanal (kanal bağlama). Web token'ı mobil uçta geçersizdir (ve tersi);
    /// böylece cookie'deki token body-tabanlı uçlardan asla "exfiltrate" edilemez. Default Web (mevcut satırlar).
    /// </summary>
    public AuthClient Client { get; set; } = AuthClient.Web;

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
