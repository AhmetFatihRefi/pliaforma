namespace Pliaforma.Domain.Common;

/// <summary>
/// Tüm entity'lerin ortak temeli: Guid Id + audit alanları + soft-delete.
/// Audit alanları AuditInterceptor tarafından doldurulur; IsDeleted global query
/// filter ile gizlenir.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
}
