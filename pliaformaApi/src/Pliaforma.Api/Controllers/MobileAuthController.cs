using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pliaforma.Api.Auth;
using Pliaforma.Api.RateLimiting;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Models;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Features.Auth;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 53 — native mobil istemci auth uçları. Web'den TEK farkı taşımadır: refresh ve güvenilir-cihaz
/// token'ları cookie yerine response BODY ile döner/alınır (mobil istemci OS güvenli deposunda saklar).
/// Bu uçlar cookie ASLA yazmaz/okumaz; kanal bağlama gereği yalnız <see cref="AuthClient.Mobile"/> ile
/// verilmiş refresh token'ları kabul eder (web cookie token'ı burada geçersizdir — XSS-exfil kapalı).
/// İş mantığı web ile AYNI command/handler'lardadır; register/register-studio mobilde bilinçli olarak yoktur.
/// </summary>
[ApiController]
[Route("api/auth/mobile")]
[Produces("application/json")]
public sealed class MobileAuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public MobileAuthController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login(MobileLoginRequest body, CancellationToken ct)
    {
        // Sprint 50 + 53: güvenilir-cihaz token'ı mobilde cookie'den değil BODY'den gelir.
        var outcome = await _dispatcher.Send(
            new LoginCommand(body.Email, body.Password, ClientIp, body.TrustedDeviceToken, AuthClient.Mobile), ct);
        return MobileOutcomeOk(outcome, "Giriş başarılı.");
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Refresh(MobileRefreshRequest body, CancellationToken ct)
    {
        var outcome = await _dispatcher.Send(
            new RefreshTokenCommand(body.RefreshToken, body.StudioId, ClientIp, AuthClient.Mobile), ct);
        return MobileOutcomeOk(outcome, "Oturum yenilendi.");
    }

    // Anonim + idempotent: body'deki refresh token (varsa) revoke edilir. Cookie işlemi YOKTUR.
    // Sprint 53 fix F4: anonim DB-yazan uç — diğer mobil uçlar gibi auth rate-limit'e tabidir.
    [AllowAnonymous]
    [HttpPost("logout")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Logout(MobileLogoutRequest body, CancellationToken ct)
    {
        await _dispatcher.Send(new LogoutCommand(body.RefreshToken, AuthClient.Mobile), ct);
        SetNoStore();
        return Ok(ApiResponse<object?>.Ok(null, "Çıkış yapıldı."));
    }

    // Anonim: login 2. faktörü. rememberDevice=true ise ham trusted token BODY'de döner (cookie YAZILMAZ);
    // mobil istemci OS güvenli deposuna koyar.
    [AllowAnonymous]
    [HttpPost("mfa/challenge/verify")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaChallengeVerify(MobileMfaChallengeVerifyRequest body, CancellationToken ct)
    {
        var outcome = await _dispatcher.Send(
            new MfaChallengeVerifyCommand(body.ChallengeToken, body.Code, body.RememberDevice ?? false, ClientIp, AuthClient.Mobile), ct);

        SetNoStore();
        return Ok(ApiResponse<MobileAuthResponse>.Ok(
            MobileAuthResponse.From(outcome.Auth,
                trustedDeviceToken: outcome.TrustedDeviceToken,
                trustedDeviceExpiresAt: outcome.TrustedDeviceExpiresAt),
            "Giriş başarılı."));
    }

    // Anonim: login 2. faktörü (kurtarma kodu). Trusted device ASLA üretilmez/dönülmez (AGENTS.md §10.1).
    [AllowAnonymous]
    [HttpPost("mfa/recovery-code/verify")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaRecoveryVerify(MobileMfaRecoveryCodeVerifyRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.Send(
            new MfaRecoveryCodeVerifyCommand(body.ChallengeToken, body.RecoveryCode, ClientIp, AuthClient.Mobile), ct);
        SetNoStore();
        return Ok(ApiResponse<MobileAuthResponse>.Ok(MobileAuthResponse.From(result), "Giriş başarılı."));
    }

    // mfa_pending token ile erişilir (MfaPendingGuardMiddleware allowlist). setup/start taşıma-bağımsızdır,
    // mobil istemci mevcut /api/auth/mfa/setup/start ucunu kullanır; verify'ın mobil varyantı token'ı body'de döner.
    [Authorize]
    [HttpPost("mfa/setup/verify")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaSetupVerify(MobileMfaSetupVerifyRequest body, CancellationToken ct)
    {
        // Sprint 53 fix F1: bu uç YALNIZ mfa_pending (kurulum bekleyen) oturumlar içindir. Tam yetkili bir
        // bearer buradan 45 günlük mobil refresh token alamaz — aksi halde web'deki bir XSS, elindeki access
        // token'ı MFA setup yolu üzerinden uzun ömürlü body token'ına "aklayabilirdi" (kanal bağlamanın
        // kapattığı yüzeyin yan kapısı). Kurulu-MFA'lı kullanıcının yeniden kurulumu web akışında kalır.
        if (User.FindFirst(AppClaims.MfaPending)?.Value != "true")
            throw new ForbiddenException("Bu uç yalnız MFA kurulumu bekleyen oturumlar içindir.");

        var r = await _dispatcher.Send(new MfaSetupVerifyCommand(body.Code, ClientIp, AuthClient.Mobile), ct);
        SetNoStore();
        return Ok(ApiResponse<MobileMfaSetupCompleteResponse>.Ok(
            new MobileMfaSetupCompleteResponse(MobileAuthResponse.From(r.Auth), r.RecoveryCodes),
            "MFA etkinleştirildi. Kurtarma kodlarınızı güvenli bir yerde saklayın (bir daha gösterilmez)."));
    }

    /// <summary>AuthOutcome → HTTP: challenge / setup-pending / normal token (hepsi no-store).</summary>
    private IActionResult MobileOutcomeOk(AuthOutcome outcome, string message)
    {
        SetNoStore();
        if (outcome.MfaChallengeRequired)
            return Ok(ApiResponse<MfaChallengeResponse>.Ok(new MfaChallengeResponse(outcome.ChallengeToken!), "MFA doğrulaması gerekli."));
        return Ok(ApiResponse<MobileAuthResponse>.Ok(MobileAuthResponse.From(outcome.Auth!, outcome.MfaSetupRequired), message));
    }

    /// <summary>
    /// Sprint 53 gatekeeper şartı: token taşıyan mobil auth yanıtları hiçbir ara katmanda (proxy/CDN/OS HTTP
    /// cache) saklanmasın. Tüm mobil auth uçlarına tekdüze uygulanır.
    /// </summary>
    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
