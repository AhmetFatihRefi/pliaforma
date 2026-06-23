using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Kullanıcının TOTP MFA ayarları (1:1, Sprint 31-MFA). Secret AES-256-GCM ile ŞİFRELİ saklanır
/// (<c>Mfa:EncryptionKey</c>). Setup sırasında doğrulanmamış secret <see cref="PendingSecretEncrypted"/>'te
/// bekler; verify başarılı olunca <see cref="SecretEncrypted"/>'e taşınır ve <see cref="EnrolledAt"/> set edilir.
/// </summary>
public class UserMfaSettings : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Aktif TOTP secret (şifreli). null → henüz aktif MFA yok.</summary>
    public string? SecretEncrypted { get; set; }

    /// <summary>Setup başlatıldı ama doğrulanmadı — bekleyen secret (şifreli).</summary>
    public string? PendingSecretEncrypted { get; set; }

    public DateTime? EnrolledAt { get; set; }

    /// <summary>Kabul edilen son TOTP zaman-adımı (replay koruması; aynı adım tekrar kabul edilmez).</summary>
    public long? LastUsedTimestep { get; set; }
}
