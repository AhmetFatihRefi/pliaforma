using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Common.Security;

/// <summary>
/// Hesap düzeyi MFA gereklilik politikası (Sprint 31-MFA): kullanıcının <b>herhangi bir aktif</b>
/// Owner/StudioAdmin üyeliği varsa MFA zorunludur (aktif-stüdyo rolüne göre değil). Master switch
/// <c>Mfa:Enabled</c> ile birlikte değerlendirilir.
/// </summary>
public static class MfaPolicy
{
    public static bool AccountRequiresMfa(IEnumerable<StudioMembership> activeMemberships) =>
        activeMemberships.Any(m => m.Status == MembershipStatus.Active
                                   && m.Role is StudioRole.Owner or StudioRole.StudioAdmin);
}
