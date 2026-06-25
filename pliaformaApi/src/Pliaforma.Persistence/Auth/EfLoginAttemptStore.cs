using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Persistence.Contexts;

namespace Pliaforma.Persistence.Auth;

/// <summary>
/// Başarısız login denemelerini YENİ bir <see cref="AppDbContext"/> (ayrı bağlantı/işlem) ile kaydeder →
/// login komutu 401/423 fırlatıp <c>TransactionBehavior</c> rollback yapsa bile sayaç/kilit KALICI olur.
/// Eşikte (AuthSettings.MaxFailedAttempts) geçici kilit (LockoutMinutes) uygular.
/// </summary>
public sealed class EfLoginAttemptStore : ILoginAttemptStore
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly IDateTime _clock;
    private readonly AuthSettings _auth;

    public EfLoginAttemptStore(DbContextOptions<AppDbContext> options, IDateTime clock, IOptions<AuthSettings> auth)
    {
        _options = options;
        _clock = clock;
        _auth = auth.Value;
    }

    public async Task<bool> RecordFailedAttemptAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Kilitleme kapalıysa (DEV) sayaç/kilit hiç oluşmasın → bilerek yanlış parola test edilebilir.
        if (!_auth.EnableLockout)
            return false;

        await using var ctx = new AppDbContext(_options);
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return false;

        user.FailedLoginCount += 1;
        var lockedNow = user.FailedLoginCount >= _auth.MaxFailedAttempts;
        if (lockedNow)
        {
            user.LockoutEnd = _clock.UtcNow.AddMinutes(_auth.LockoutMinutes);
            user.FailedLoginCount = 0;   // kilit bitiminde temiz başlasın
        }

        await ctx.SaveChangesAsync(cancellationToken);
        return lockedNow;
    }
}
