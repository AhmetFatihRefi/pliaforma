using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Auth;

/// <summary>
/// Geçerli bir refresh token ile yeni access token üretir ve refresh token'ı rotasyonlar
/// (eskisini revoke eder, yenisini verir, zinciri ReplacedByTokenHash ile bağlar). Üretim sırasında
/// kullanıcı + (varsa) seçilen stüdyodaki üyelik HÂLÂ Active mi DB'den doğrulanır.
/// <para>Sprint 53: kanal bağlama (Client) + reuse tespiti — yalnız ROTASYONLA TÜKETİLMİŞ token'ın
/// yeniden sunumu hırsızlık sinyalidir: grace (60 sn) dışında kullanıcının TÜM aktif refresh token'ları
/// iptal edilir; grace içinde aile korunur ama audit yazılır. Logout/toplu iptalle revoke edilmiş
/// (zincirsiz) token sessiz 401 alır (yanlış-pozitif kilitleme yok). Rotasyon atomiktir (koşullu UPDATE).</para>
/// </summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    Guid? StudioId = null,
    string? Ip = null,
    AuthClient Client = AuthClient.Web) : ICommand<AuthOutcome>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenHandler : ICommandHandler<RefreshTokenCommand, AuthOutcome>
{
    private const string InvalidSessionMessage = "Oturum geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapın.";

    /// <summary>
    /// Sprint 53 — meşru yarış payı: rotasyondan/logout'tan sonraki 60 sn içinde eski token'ın yeniden
    /// sunulması (iki sekmenin eşzamanlı refresh'i, logout+refresh yarışı) aile iptali TETİKLEMEZ (yalnız 401).
    /// </summary>
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(60);

    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTime _clock;
    private readonly AuthSessionService _session;
    private readonly ISecurityAuditLogger _audit;
    private readonly IRefreshTokenFamilyRevoker _familyRevoker;

    public RefreshTokenHandler(IAppDbContext db, ITokenService tokens, IDateTime clock, AuthSessionService session,
        ISecurityAuditLogger audit, IRefreshTokenFamilyRevoker familyRevoker)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _session = session;
        _audit = audit;
        _familyRevoker = familyRevoker;
    }

    public async Task<AuthOutcome> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
            throw new UnauthorizedException(InvalidSessionMessage);

        // Sprint 53: kanal bağlama — web (cookie) token'ı mobil uçta, mobil token web uçta geçersizdir.
        // Mesaj generic (kanal bilgisi sızdırılmaz).
        if (stored.Client != request.Client)
            throw new UnauthorizedException(InvalidSessionMessage);

        // Sprint 53 (fix F2+F3): iptal edilmiş token yeniden sunuldu → reuse karar ağacı (aşağıda).
        if (stored.RevokedAt is { } revokedAt)
            throw await ReuseDetectedAsync(stored, revokedAt, request.Client, cancellationToken);

        if (_clock.UtcNow >= stored.ExpiresAt)
            throw new UnauthorizedException(InvalidSessionMessage);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken)
            ?? throw new UnauthorizedException("Oturum geçersiz.");

        if (!user.IsActive)
            throw new ForbiddenException("Hesabınız devre dışı bırakılmış.");

        var activeMemberships = await _db.StudioMemberships
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .ToListAsync(cancellationToken);

        // İstenen stüdyo bağlamı varsa üyeliğin hâlâ Active olduğunu doğrula.
        StudioMembership? membership = null;
        if (request.StudioId is { } studioId)
        {
            membership = activeMemberships.FirstOrDefault(m => m.StudioId == studioId)
                ?? throw new ForbiddenException("Stüdyo üyeliğiniz artık aktif değil.");
        }

        // Rotasyon (Sprint 53 fix F5 — ATOMİK): yalnız hâlâ tüketilmemiş (RevokedAt IS NULL) token revoke
        // edilebilir. Koşullu UPDATE 0 satır etkilediyse aynı token'la eşzamanlı bir rotasyon yarışı kazanmıştır
        // (double-spend); bu istek reuse yoluna düşer. Aynı token'dan ASLA iki başarılı rotasyon çıkamaz.
        // ExecuteUpdate ambient transaction'a katılır; kilit beklemesi yarışın serileşmesini garanti eder.
        var revokeMoment = _clock.UtcNow;
        var affected = await _db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, revokeMoment), cancellationToken);

        if (affected == 0)
            throw await ReuseDetectedAsync(stored, revokeMoment, request.Client, cancellationToken, consumedByRace: true);

        var outcome = await _session.IssuePostAuthAsync(user, membership, activeMemberships, request.Ip, cancellationToken, request.Client);

        // Sprint 53: rotasyon zinciri — reuse tespitinin dayanağı. IssuePostAuthAsync her zaman Auth döner
        // (challenge yolu yok). TransactionBehavior komutu sardığından tüm yazımlar atomiktir.
        stored.ReplacedByTokenHash = _tokens.HashRefreshToken(outcome.Auth!.RefreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return outcome;
    }

    /// <summary>
    /// Sprint 53 fix F2+F3 — iptal edilmiş token sunumunun karar ağacı:
    /// <list type="bullet">
    /// <item><b>Zincirsiz</b> (ReplacedByTokenHash == null: logout / parola değişimi / toplu iptal / aile iptali) →
    /// bayat ama muhtemelen DÜRÜST istemci: sessiz 401. Aile iptali ve alarm YOK (yanlış-pozitif kilitleme döngüsü
    /// engellenir — F2).</item>
    /// <item><b>Zincirli</b> (rotasyonla tüketilmiş; ya ReplacedByTokenHash != null ya da bu istek koşullu
    /// rotasyonu kaybetti) → gerçek hırsızlık sinyali. Grace (60 sn) İÇİNDE: meşru yarış payı — aile korunur ama
    /// audit YAZILIR (grace=true; F3, sinyal kaybolmaz). Grace DIŞINDA: aile iptali + audit (grace=false).</item>
    /// </list>
    /// Aile iptali ve audit AYRI bağlamla yazılır → dönen 401'in transaction rollback'inden ETKİLENMEZ.
    /// Detail'e ham token/tam hash asla yazılmaz (yalnız 8 karakterlik maskeli önek).
    /// </summary>
    private async Task<UnauthorizedException> ReuseDetectedAsync(
        Domain.Entities.RefreshToken stored, DateTime revokedAt, AuthClient client,
        CancellationToken cancellationToken, bool consumedByRace = false)
    {
        var consumedByRotation = consumedByRace || stored.ReplacedByTokenHash is not null;
        if (!consumedByRotation)
            return new UnauthorizedException(InvalidSessionMessage);

        var withinGrace = consumedByRace || _clock.UtcNow - revokedAt <= ReuseGrace;
        if (!withinGrace)
            await _familyRevoker.RevokeAllActiveAsync(stored.UserId, _clock.UtcNow, cancellationToken);

        await _audit.LogAsync(SecurityAuditEventType.RefreshTokenReuseDetected, SecurityAuditOutcome.Failure,
            actorUserId: stored.UserId,
            detail: $"client={client};grace={(withinGrace ? "true" : "false")};token_prefix={stored.TokenHash[..8]}",
            cancellationToken: cancellationToken);

        return new UnauthorizedException(InvalidSessionMessage);
    }
}
