using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Tek-kullanımlık MFA recovery code (Sprint 31-MFA). Plain değer SAKLANMAZ — yalnız <see cref="CodeHash"/>
/// (SHA-256). Kullanılınca <see cref="ConsumedAt"/> set edilir; aynı kod ikinci kez çalışmaz. Setup sonrası
/// plain kodlar kullanıcıya BİR KEZ gösterilir.
/// </summary>
public class MfaRecoveryCode : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string CodeHash { get; set; } = default!;
    public DateTime? ConsumedAt { get; set; }
}
