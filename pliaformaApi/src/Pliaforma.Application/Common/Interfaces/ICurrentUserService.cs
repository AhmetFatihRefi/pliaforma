using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Mevcut isteğin kimlik bağlamı (JWT claim'lerinden çözülür). Tenant izolasyonu
/// StudioId, yetkilendirme StudioRole/GlobalRole ile yapılır. Kritik yazma
/// işlemlerinde claim'e kör güvenilmez; MembershipGuardBehavior DB'den doğrular.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    GlobalRole? GlobalRole { get; }

    Guid? StudioId { get; }
    StudioRole? StudioRole { get; }
    Guid? MembershipId { get; }

    bool IsAuthenticated { get; }

    Guid RequireUserId();
    Guid RequireStudioId();
}
