using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Cqrs;

namespace Pliaforma.Application.Common.Behaviors;

/// <summary>Her isteğin adını ve süresini loglar; hataları yakalayıp tekrar fırlatır.</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation("İstek tamamlandı: {Request} ({Elapsed} ms)", requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "İstek hata verdi: {Request} ({Elapsed} ms)", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
