using Pliaforma.Domain.Common;

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

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
