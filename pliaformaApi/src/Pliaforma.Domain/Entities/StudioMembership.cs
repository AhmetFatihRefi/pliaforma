using Pliaforma.Domain.Common;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir kullanıcıyı bir stüdyoya bağlayan üyelik. "Kim bu stüdyoya hangi yetkiyle
/// erişir"in tek kaynağıdır. Tenant-scoped'tır (StudioId taşır).
/// </summary>
public class StudioMembership : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public StudioRole Role { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Pending;
    public MembershipDirection Direction { get; set; }

    /// <summary>Üyeliğin aktifleştiği (kabul edildiği) an.</summary>
    public DateTime? AcceptedAt { get; set; }
}
