using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Features.Auth;

/// <summary>Yeni güvenilir cihaz için istemciye dönecek ham token + son kullanım tarihi.</summary>
public sealed record TrustedDeviceIssue(string RawToken, DateTime ExpiresAt);

/// <summary>
/// Sprint 50 — "Bu cihazı hatırla": MFA'yı 30 gün atlayan güvenilir cihaz kayıtları. Cookie'deki ham opak token
/// yalnız hash olarak saklanır (DB sızıntısında düz elde edilemez). MFA'yı KAPATMAZ; yalnız aynı cihazda süre
/// boyunca ikinci faktörü atlar.
/// </summary>
public sealed class TrustedMfaDeviceService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;
    private readonly MfaSettings _mfa;

    public TrustedMfaDeviceService(IAppDbContext db, IDateTime clock, IOptions<MfaSettings> mfa)
    {
        _db = db;
        _clock = clock;
        _mfa = mfa.Value;
    }

    /// <summary>
    /// Verilen ham cookie token'ı bu kullanıcı için GEÇERLİ bir güvenilir cihaza mı ait? Eşleşirse
    /// LastUsedAt güncellenir (süre UZATILMAZ — fixed window) ve true döner. Token yok/yanlış/expired/revoked → false.
    /// </summary>
    public async Task<bool> IsTrustedAsync(Guid userId, string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var hash = OpaqueToken.Hash(rawToken);
        var now = _clock.UtcNow;

        var device = await _db.UserTrustedMfaDevices.FirstOrDefaultAsync(
            d => d.UserId == userId && d.TokenHash == hash && d.RevokedAt == null && d.ExpiresAt > now, ct);

        if (device is null)
            return false;

        device.LastUsedAt = now;   // SaveChanges çağrı sahibinde (IssueAsync) yapılır
        return true;
    }

    /// <summary>
    /// Yeni bir güvenilir cihaz kaydı EKLER (ham token'ı hash'leyerek) ve ham token'ı döner. Kayıt, çağrı
    /// sahibinin mevcut SaveChanges'ı ile kalıcılaşır. Süre config'ten (<c>Mfa:TrustedDeviceDays</c>, vars. 30).
    /// </summary>
    public TrustedDeviceIssue Remember(Guid userId, string? ip)
    {
        var raw = OpaqueToken.Generate();
        var expires = _clock.UtcNow.AddDays(Math.Max(1, _mfa.TrustedDeviceDays));

        _db.UserTrustedMfaDevices.Add(new UserTrustedMfaDevice
        {
            UserId = userId,
            TokenHash = OpaqueToken.Hash(raw),
            ExpiresAt = expires,
            CreatedByIp = ip,
        });

        return new TrustedDeviceIssue(raw, expires);
    }

    /// <summary>
    /// Kullanıcının TÜM aktif güvenilir cihazlarını iptal eder (parola değişimi / MFA reset / yeniden kurulum).
    /// SaveChanges çağrı sahibinde yapılır.
    /// </summary>
    public async Task RevokeAllAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var devices = await _db.UserTrustedMfaDevices
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var d in devices)
            d.RevokedAt = nowUtc;
    }
}
