using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Auth;

/// <summary>
/// Sprint 53 — refresh token reuse tespitinde kullanıcının TÜM aktif refresh token'larını YENİ bir
/// <see cref="AppDbContext"/> (ayrı bağlantı/işlem) ile iptal eder → reuse yolu 401 fırlatıp
/// <c>TransactionBehavior</c> rollback yapsa bile iptaller KALICI olur (EfLoginAttemptStore deseni).
/// </summary>
public sealed class EfRefreshTokenFamilyRevoker : IRefreshTokenFamilyRevoker
{
    private readonly DbContextOptions<AppDbContext> _options;

    public EfRefreshTokenFamilyRevoker(DbContextOptions<AppDbContext> options) => _options = options;

    public async Task RevokeAllActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await using var ctx = new AppDbContext(_options);
        var active = await ctx.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var t in active)
            t.RevokedAt = nowUtc;

        await ctx.SaveChangesAsync(cancellationToken);
    }
}
