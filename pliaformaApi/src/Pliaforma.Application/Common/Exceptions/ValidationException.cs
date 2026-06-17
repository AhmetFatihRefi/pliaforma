using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Common.Exceptions;

/// <summary>
/// FluentValidation hatalarını taşır. ExceptionHandlingMiddleware bunu HTTP 400 +
/// alan bazlı hata sözlüğüne dönüştürür.
/// </summary>
public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Doğrulama hatası.")
    {
        Errors = errors;
    }
}
