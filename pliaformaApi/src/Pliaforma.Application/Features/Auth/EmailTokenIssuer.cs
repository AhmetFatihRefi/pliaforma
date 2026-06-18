using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>Sprint 16 token süreleri + cooldown (kullanıcı onaylı).</summary>
public static class AuthTokenRules
{
    public static readonly TimeSpan EmailVerificationTtl = TimeSpan.FromHours(48);
    public static readonly TimeSpan PasswordResetTtl = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Tek-kullanımlık auth token üretir/tüketir (Sprint 16). Yeni token üretirken aynı purpose'un eski
/// kullanılmamış tokenlarını **soft-delete ile iptal eder**. Ham token yalnız döner (DB'de hash). SaveChanges
/// çağırMAZ — çağıranın transaction'ında. Tüketimde expired/used/invalid → generic <see cref="BusinessRuleException"/>.
/// </summary>
public sealed class EmailTokenIssuer
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;

    public EmailTokenIssuer(IAppDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Eski kullanılmamış tokenları iptal eder, yeni token oluşturur ve ham değeri döner.</summary>
    public async Task<string> IssueAsync(
        Guid userId, AuthTokenPurpose purpose, TimeSpan ttl, string? ip, string? userAgent, CancellationToken ct)
    {
        var actives = await _db.AuthTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ToListAsync(ct);
        foreach (var t in actives)
            _db.AuthTokens.Remove(t); // AuditInterceptor → soft delete (eski link geçersizleşir)

        var raw = OpaqueToken.Generate();
        _db.AuthTokens.Add(new AuthToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = OpaqueToken.Hash(raw),
            ExpiresAt = _clock.UtcNow.Add(ttl),
            CreatedIp = ip,
            UserAgent = userAgent,
        });
        return raw;
    }

    /// <summary>Son <paramref name="window"/> içinde aynı purpose için token üretilmiş mi? (soft-deleted dahil.)</summary>
    public async Task<bool> InCooldownAsync(Guid userId, AuthTokenPurpose purpose, TimeSpan window, CancellationToken ct)
    {
        var since = _clock.UtcNow.Subtract(window);
        return await _db.AuthTokens.IgnoreQueryFilters()
            .AnyAsync(t => t.UserId == userId && t.Purpose == purpose && t.CreatedAt > since, ct);
    }

    /// <summary>Ham token'ı doğrular + tüketir (ConsumedAt set). Geçersiz/expired/used → generic hata.</summary>
    public async Task<AuthToken> ConsumeAsync(string rawToken, AuthTokenPurpose purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new BusinessRuleException("Bağlantı geçersiz veya süresi dolmuş.");

        var hash = OpaqueToken.Hash(rawToken.Trim());
        var token = await _db.AuthTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);

        if (token is null || token.ConsumedAt is not null || token.ExpiresAt <= _clock.UtcNow)
            throw new BusinessRuleException("Bağlantı geçersiz veya süresi dolmuş.");

        token.ConsumedAt = _clock.UtcNow;
        return token;
    }
}
