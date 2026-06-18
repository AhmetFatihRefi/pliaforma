using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Auth akışlarının ortak adımları: token üretimi + refresh token kaydı + üyelik
/// listesi/aktif stüdyo bağlamının oluşturulması. register/login/refresh/switch
/// handler'ları bunu kullanır (mantığı tek yerde tutar).
/// </summary>
public sealed class AuthSessionService
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public AuthSessionService(IAppDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    /// <summary>
    /// Kullanıcı için access + refresh token üretir, refresh'i hash'leyip kaydeder ve
    /// üyelik listesi + (varsa) aktif stüdyo bağlamı ile AuthResult döner.
    /// </summary>
    public async Task<AuthResult> IssueAsync(User user, StudioMembership? activeMembership, string? ip, CancellationToken ct)
    {
        var refresh = _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ip,
        });
        await _db.SaveChangesAsync(ct);

        return await BuildResultAsync(user, activeMembership, refresh.RawToken, refresh.ExpiresAt, ct);
    }

    /// <summary>
    /// Yeni refresh token ÜRETMEDEN yalnızca access token üretir (switch-studio için).
    /// Sonuçtaki RefreshToken boştur; controller bu durumda cookie'ye dokunmaz.
    /// </summary>
    public async Task<AuthResult> IssueAccessOnlyAsync(User user, StudioMembership? activeMembership, CancellationToken ct) =>
        await BuildResultAsync(user, activeMembership, refreshRawToken: string.Empty, refreshExpiresAt: default, ct);

    private async Task<AuthResult> BuildResultAsync(User user, StudioMembership? activeMembership, string refreshRawToken, DateTime refreshExpiresAt, CancellationToken ct)
    {
        var access = _tokens.GenerateAccessToken(user, activeMembership);
        var memberships = await LoadMembershipsAsync(user.Id, ct);
        var active = await BuildActiveStudioAsync(activeMembership?.Id, ct);

        return new AuthResult(
            access.Token,
            access.ExpiresAt,
            refreshRawToken,
            refreshExpiresAt,
            ToUserDto(user),
            active,
            memberships);
    }

    public async Task<IReadOnlyList<MembershipDto>> LoadMembershipsAsync(Guid userId, CancellationToken ct) =>
        await _db.StudioMemberships
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.Studio!.Name)
            .Select(m => new MembershipDto(
                m.Id, m.StudioId, m.Studio!.Name, m.Studio.Slug, m.Role, m.Status))
            .ToListAsync(ct);

    public async Task<ActiveStudioDto?> BuildActiveStudioAsync(Guid? membershipId, CancellationToken ct)
    {
        if (membershipId is null)
            return null;

        return await _db.StudioMemberships
            .Where(m => m.Id == membershipId && m.Status == MembershipStatus.Active)
            .Select(m => new ActiveStudioDto(
                m.Id, m.StudioId, m.Studio!.Name, m.Studio.Slug, m.Role))
            .FirstOrDefaultAsync(ct);
    }

    public static UserDto ToUserDto(User user) =>
        new(user.Id, user.Email, user.FullName, user.Phone, user.GlobalRole, user.EmailVerifiedAt is not null);

    /// <summary>Kullanıcının tüm aktif refresh tokenlarını iptal eder (şifre sıfırlama sonrası — Sprint 16).</summary>
    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active)
            t.RevokedAt = nowUtc;
    }
}
