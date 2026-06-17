namespace Pliaforma.Application.Common.Interfaces;

/// <summary>Test edilebilirlik için zaman soyutlaması.</summary>
public interface IDateTime
{
    DateTime UtcNow { get; }
}
