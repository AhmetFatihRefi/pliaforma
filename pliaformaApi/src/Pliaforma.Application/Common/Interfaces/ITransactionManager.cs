namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Veritabanı transaction yönetimi için sağlayıcıdan bağımsız soyutlama. EF Core'un
/// DatabaseFacade'i Application'a sızmasın diye TransactionBehavior bunu kullanır.
/// </summary>
public interface ITransactionManager
{
    /// <summary>Şu an aktif bir transaction var mı? (iç içe transaction'ı önlemek için.)</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Yeni bir transaction başlatır ve commit/rollback/dispose için kapsayıcı döner.</summary>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Tek bir transaction'ın yaşam döngüsü.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
