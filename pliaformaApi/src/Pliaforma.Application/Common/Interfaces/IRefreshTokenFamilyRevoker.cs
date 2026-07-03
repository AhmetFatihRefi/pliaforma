namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Sprint 53 — refresh token reuse (olası hırsızlık) tespitinde kullanıcının TÜM aktif refresh
/// token'larını iptal eder. Somut uygulama BAĞIMSIZ DbContext ile yazar: reuse yolu 401 fırlatıp
/// ana komut transaction'ı rollback olsa bile iptaller kalıcıdır (ILoginAttemptStore deseni).
/// </summary>
public interface IRefreshTokenFamilyRevoker
{
    Task RevokeAllActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
