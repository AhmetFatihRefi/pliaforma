using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Platform;

/// <summary>
/// Platform (cross-tenant) işlemleri için ortak kurallar. Yetki global_role üzerinden kontrol edilir;
/// stüdyo bağlamı (studio_id) GEREKMEZ. Bu, controller policy'sinin yanında ikinci bir savunma katmanıdır.
/// </summary>
internal static class PlatformRules
{
    /// <summary>İstek sahibinin GlobalRole'ü PlatformAdmin değilse 403 fırlatır.</summary>
    public static void EnsurePlatformAdmin(ICurrentUserService currentUser)
    {
        if (currentUser.GlobalRole != GlobalRole.PlatformAdmin)
            throw new ForbiddenException("Bu işlem yalnızca platform yöneticisi tarafından yapılabilir.");
    }
}
