using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Application.Common.Behaviors;

/// <summary>
/// Komutları (ICommand) tek bir transaction içine alır. Sorgular ve hâlihazırda açık
/// transaction olan durumlar atlanır. EF Core sağlayıcı detayları ITransactionManager
/// soyutlaması arkasında kalır.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITransactionManager _transactions;

    public TransactionBehavior(ITransactionManager transactions) => _transactions = transactions;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var isCommand = typeof(TRequest).GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

        if (!isCommand || _transactions.HasActiveTransaction)
            return await next();

        await using var transaction = await _transactions.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
