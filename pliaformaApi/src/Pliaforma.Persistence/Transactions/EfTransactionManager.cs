using Microsoft.EntityFrameworkCore.Storage;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Transactions;

/// <summary>
/// ITransactionManager'ın EF Core uygulaması. DatabaseFacade burada kalır; Application
/// katmanı yalnızca soyut arayüzü görür.
/// </summary>
public sealed class EfTransactionManager : ITransactionManager
{
    private readonly AppDbContext _db;

    public EfTransactionManager(AppDbContext db) => _db = db;

    public bool HasActiveTransaction => _db.Database.CurrentTransaction is not null;

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(transaction);
    }

    private sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;

        public EfTransactionScope(IDbContextTransaction transaction) => _transaction = transaction;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
