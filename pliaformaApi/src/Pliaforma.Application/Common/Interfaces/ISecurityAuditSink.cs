using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// SecurityAuditLog kaydını <b>bağımsız bir bağlam/işlemde</b> yazar. Böylece çağıran komutun transaction'ı
/// geri alınsa bile (ör. başarısız login 401 fırlatınca <c>TransactionBehavior</c> rollback yapar) denetim
/// kaydı KALICI olur. Best-effort: yazım hatası çağıran akışı KIRMAZ.
/// </summary>
public interface ISecurityAuditSink
{
    Task WriteAsync(SecurityAuditLog entry, CancellationToken cancellationToken = default);
}
