using System.Net;
using System.Text.Json;
using Pliaforma.Application.Common.Models;
using Pliaforma.Domain.Exceptions;
using ValidationException = Pliaforma.Application.Common.Exceptions.ValidationException;

namespace Pliaforma.Api.Middleware;

/// <summary>Tüm istisnaları yakalar ve standart ApiResponse hata gövdesine çevirir.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteAsync(context, ex);
        }
    }

    private async Task WriteAsync(HttpContext context, Exception ex)
    {
        var (status, message, errors) = ex switch
        {
            ValidationException ve => (HttpStatusCode.BadRequest, ve.Message, ve.Errors),
            UnauthorizedException => (HttpStatusCode.Unauthorized, ex.Message, null),
            ForbiddenException => (HttpStatusCode.Forbidden, ex.Message, null),
            NotFoundException => (HttpStatusCode.NotFound, ex.Message, null),
            ConflictException => (HttpStatusCode.Conflict, ex.Message, null),
            BusinessRuleException => (HttpStatusCode.Conflict, ex.Message, null),
            GoneException => (HttpStatusCode.Gone, ex.Message, null),
            _ => (HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu.", (IReadOnlyDictionary<string, string[]>?)null),
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "İşlenmeyen istisna");

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var payload = ApiResponse<object>.Fail(message, errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
