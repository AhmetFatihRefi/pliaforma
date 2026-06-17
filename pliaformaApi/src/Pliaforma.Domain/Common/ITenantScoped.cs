namespace Pliaforma.Domain.Common;

/// <summary>
/// Bir entity'nin ait olduğu stüdyoyu (tenant) belirtir. Bu arayüzü uygulayan her
/// entity, hem global query filter hem de handler seviyesindeki kontrollerle
/// stüdyo bazında izole edilir.
/// </summary>
public interface ITenantScoped
{
    Guid StudioId { get; }
}
