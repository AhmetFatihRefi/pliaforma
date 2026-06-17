namespace Pliaforma.Application.Common.Cqrs;

/// <summary>Bir isteğin (komut/sorgu) yanıt tipini taşıyan işaretçi arayüz.</summary>
public interface IRequest<TResponse> { }

/// <summary>Durum değiştiren (yazma) istek.</summary>
public interface ICommand<TResponse> : IRequest<TResponse> { }

/// <summary>Durum değiştirmeyen (okuma) istek.</summary>
public interface IQuery<TResponse> : IRequest<TResponse> { }

/// <summary>Bir isteği işleyen handler sözleşmesi.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse> { }

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse> { }

/// <summary>Pipeline behavior'da bir sonraki adımı temsil eden delege.</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>İstek/handler arasında çalışan kesişen (cross-cutting) davranış.</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>İstekleri ilgili handler'a (ve behavior zincirine) yönlendiren bileşen.</summary>
public interface IDispatcher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
