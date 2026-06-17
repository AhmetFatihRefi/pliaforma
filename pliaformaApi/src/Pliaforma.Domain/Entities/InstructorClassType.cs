using Pliaforma.Domain.Common;

namespace Pliaforma.Domain.Entities;

/// <summary>
/// Hoca ↔ ClassType (pilates grubu) çoka-çok bağı. Hangi hocanın hangi grupta ders verdiğini
/// belirtir; opsiyonel olarak o grup için ücret override eder.
/// </summary>
public class InstructorClassType : BaseEntity, ITenantScoped
{
    public Guid StudioId { get; set; }

    public Guid InstructorProfileId { get; set; }
    public InstructorProfile? InstructorProfile { get; set; }

    public Guid ClassTypeId { get; set; }
    public ClassType? ClassType { get; set; }

    /// <summary>Bu grup için hoca ücretini override eder (yoksa InstructorProfile.Rate geçerli).</summary>
    public decimal? RateOverride { get; set; }
}
