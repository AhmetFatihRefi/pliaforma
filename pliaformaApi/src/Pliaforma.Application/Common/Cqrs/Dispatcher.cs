using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Pliaforma.Application.Common.Cqrs;

/// <summary>
/// Reflection tabanlı, MediatR'sız hafif dispatcher. Handler ve behavior tiplerini
/// önbelleğe alır; behavior zincirini (Logging → Validation → ...) handler etrafında kurar.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Type> BehaviorTypeCache = new();

    public Dispatcher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        var handlerType = HandlerTypeCache.GetOrAdd(requestType,
            _ => typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType));
        var behaviorType = BehaviorTypeCache.GetOrAdd(requestType,
            _ => typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType));

        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"'{requestType.Name}' için handler kayıtlı değil.");

        var handlerMethod = handlerType.GetMethod("Handle")!;
        RequestHandlerDelegate<TResponse> pipeline = () =>
            (Task<TResponse>)handlerMethod.Invoke(handler, [request, cancellationToken])!;

        var behaviorMethod = behaviorType.GetMethod("Handle")!;
        var behaviors = _serviceProvider.GetServices(behaviorType).Cast<object>().Reverse();

        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () =>
                (Task<TResponse>)behaviorMethod.Invoke(behavior, [request, next, cancellationToken])!;
        }

        return pipeline();
    }
}
