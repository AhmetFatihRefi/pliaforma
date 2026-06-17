using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Bir <see cref="PackagePlan"/>'ın geçerli olduğu ders grubu (<see cref="ClassType"/>). Yalnızca plan
/// <c>AllowAllClassTypes=false</c> iken anlamlıdır; o zaman plan SADECE bu kayıtlardaki class type'larda
/// kullanılabilir. <c>AllowAllClassTypes=true</c> iken bu tablo dikkate alınmaz (tüm gruplarda geçerli).
/// Tenant-scoped (StudioId denormalize). Soft-delete + IsActive ile yönetilir.
/// </summary>
public class PackagePlanClassType : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid PackagePlanId { get; set; }
    public PackagePlan? PackagePlan { get; set; }

    public Guid ClassTypeId { get; set; }
    public ClassType? ClassType { get; set; }

    public bool IsActive { get; set; } = true;
}
