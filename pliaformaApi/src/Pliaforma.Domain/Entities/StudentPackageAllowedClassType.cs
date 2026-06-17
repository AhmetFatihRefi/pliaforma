using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Satılan bir <see cref="StudentPackage"/>'ın geçerli olduğu ders grubu — satın alma anında plandan
/// **snapshot'lanır** (plan sonradan değişse bile öğrencinin paketi etkilenmez). Yalnızca paket
/// <c>AllowAllClassTypesSnapshot=false</c> iken anlamlıdır. Tenant-scoped (StudioId denormalize).
/// </summary>
public class StudentPackageAllowedClassType : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }
    public Studio? Studio { get; set; }

    public Guid StudentPackageId { get; set; }
    public StudentPackage? StudentPackage { get; set; }

    public Guid ClassTypeId { get; set; }
    public ClassType? ClassType { get; set; }
}
