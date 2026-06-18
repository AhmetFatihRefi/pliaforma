using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Global kimlik. Tenant-scoped DEĞİLDİR: bir kullanıcı tek hesapla birden fazla
/// stüdyoya StudioMembership üzerinden bağlanabilir. E-posta global benzersizdir.
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = default!;
    public string NormalizedEmail { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }

    /// <summary>Platform seviyesindeki rol (stüdyo rolü ayrı: StudioMembership.Role).</summary>
    public GlobalRole GlobalRole { get; set; } = GlobalRole.Standard;

    /// <summary>
    /// Kayıt niyeti (öğrenci/hoca). **Yalnızca UX** — stüdyosuz kullanıcının /join-studio ekranını belirler
    /// (Sprint 17 Rev2). Yetki kaynağı DEĞİL. Eski kullanıcılarda Unknown.
    /// </summary>
    public RegistrationIntent RegistrationIntent { get; set; } = RegistrationIntent.Unknown;

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// E-posta doğrulandığı an (UTC). null → doğrulanmamış (Sprint 16). Mevcut kullanıcılar migration
    /// backfill ile doğrulanmış sayılır. Doğrulanmamış kullanıcı login olabilir ama stüdyoya başvuramaz.
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    public ICollection<StudioMembership> Memberships { get; set; } = new List<StudioMembership>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
