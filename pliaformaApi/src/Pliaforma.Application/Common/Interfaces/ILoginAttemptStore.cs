namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Başarısız login denemelerini <b>bağımsız bir bağlamda</b> kaydeder. Login komutu 401/423 fırlatıp
/// <c>TransactionBehavior</c> rollback yapsa bile sayaç/kilit KALICI olur. Eşik (AuthSettings.MaxFailedAttempts)
/// aşılırsa geçici kilit uygular ve <c>true</c> döner.
/// </summary>
public interface ILoginAttemptStore
{
    /// <returns>Bu deneme ile hesap kilitlendiyse true.</returns>
    Task<bool> RecordFailedAttemptAsync(Guid userId, CancellationToken cancellationToken = default);
}
