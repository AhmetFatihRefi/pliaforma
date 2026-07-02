using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Pliaforma.Api.Auth;
using Pliaforma.Api.RateLimiting;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Models;
using Pliaforma.Application.Common.Security;
using Pliaforma.Application.Common.Settings;
using Pliaforma.Application.Features.Auth;

namespace Pliaforma.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly bool _secureCookie;
    private readonly RegistrationSettings _registration;

    public AuthController(IDispatcher dispatcher, IHostEnvironment env, IOptions<RegistrationSettings> registration)
    {
        _dispatcher = dispatcher;
        // Geliştirmede HTTP üzerinden çalışıldığı için Secure=false; üretimde HTTPS zorunlu.
        _secureCookie = !env.IsDevelopment();
        _registration = registration.Value;
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    // Sprint 49: anonim public flag — frontend "Stüdyo oluştur" yüzeylerini buna göre gizler.
    // Güvenlik sınırı DEĞİL (register-studio handler'ında ayrıca zorlanır); yalnız UI'ya ipucu verir.
    [AllowAnonymous]
    [HttpGet("registration-status")]
    public IActionResult RegistrationStatus() =>
        Ok(ApiResponse<RegistrationStatusResponse>.Ok(new RegistrationStatusResponse(_registration.StudioRegistrationEnabled)));

    [AllowAnonymous]
    [HttpPost("register-studio")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> RegisterStudio(RegisterStudioRequest body, CancellationToken ct)
    {
        var outcome = await _dispatcher.Send(
            new RegisterStudioCommand(body.StudioName, body.OwnerFullName, body.Email, body.Password, body.Phone, ClientIp), ct);
        return OutcomeOk(outcome, "Stüdyo oluşturuldu.");
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Register(RegisterRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.Send(
            new RegisterCommand(body.FullName, body.Email, body.Password, body.Phone,
                body.Intent ?? Domain.Enums.RegistrationIntent.Unknown, ClientIp), ct);
        return AuthOk(result, mfaSetupRequired: false, "Hesap oluşturuldu.");
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login(LoginRequest body, CancellationToken ct)
    {
        // Sprint 50: geçerli "bu cihazı hatırla" cookie'si varsa MFA challenge atlanır (handler doğrular).
        var trustedDeviceToken = TrustedDeviceCookie.Read(Request);
        var outcome = await _dispatcher.Send(new LoginCommand(body.Email, body.Password, ClientIp, trustedDeviceToken), ct);
        return OutcomeOk(outcome, "Giriş başarılı.");
    }

    [Authorize]
    [HttpPost("switch-studio")]
    public async Task<IActionResult> SwitchStudio(SwitchStudioRequest body, CancellationToken ct)
    {
        // Access-only: refresh token'a dokunulmaz, cookie aynı kalır.
        var result = await _dispatcher.Send(new SwitchStudioCommand(body.StudioId), ct);
        return Ok(ApiResponse<AuthResponse>.Ok(AuthResponse.From(result), "Stüdyo değiştirildi."));
    }

    // Anonim: JWT yok, oturum httpOnly refresh cookie ile yenilenir.
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Refresh(RefreshRequest body, CancellationToken ct)
    {
        var raw = RefreshCookie.Read(Request)
            ?? throw new Domain.Exceptions.UnauthorizedException("Oturum bulunamadı. Lütfen tekrar giriş yapın.");

        var outcome = await _dispatcher.Send(new RefreshTokenCommand(raw, body.StudioId, ClientIp), ct);
        return OutcomeOk(outcome, "Oturum yenilendi.");
    }

    // Anonim + idempotent: access token süresi dolmuş olsa bile cookie temizlenebilmeli.
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var raw = RefreshCookie.Read(Request);
        await _dispatcher.Send(new LogoutCommand(raw), ct);
        RefreshCookie.Clear(Response, _secureCookie);
        return Ok(ApiResponse<object?>.Ok(null, "Çıkış yapıldı."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await _dispatcher.Send(new GetMeQuery(), ct);
        return Ok(ApiResponse<MeResult>.Ok(result));
    }

    // ---- Sprint 16: e-posta doğrulama + şifre sıfırlama ----

    [AllowAnonymous]
    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest body, CancellationToken ct)
    {
        await _dispatcher.Send(new VerifyEmailCommand(body.Token), ct);
        return Ok(ApiResponse<object?>.Ok(null, "E-posta adresiniz doğrulandı."));
    }

    [Authorize]
    [HttpPost("resend-verification-email")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        await _dispatcher.Send(new ResendVerificationEmailCommand(), ct);
        return Ok(ApiResponse<object?>.Ok(null, "Doğrulama e-postası gönderildi (zaten doğrulanmışsa yeni e-posta gönderilmez)."));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest body, CancellationToken ct)
    {
        await _dispatcher.Send(new ForgotPasswordCommand(body.Email, ClientIp), ct);
        return Ok(ApiResponse<object?>.Ok(null, "Eğer bu e-posta ile kayıt varsa şifre sıfırlama bağlantısı gönderildi."));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest body, CancellationToken ct)
    {
        await _dispatcher.Send(new ResetPasswordCommand(body.Token, body.NewPassword), ct);
        return Ok(ApiResponse<object?>.Ok(null, "Şifreniz güncellendi. Lütfen yeni şifrenizle giriş yapın."));
    }

    // Sprint 31B-2: oturum açmış kullanıcı kendi parolasını değiştirir (mevcut parola doğrulanır).
    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest body, CancellationToken ct)
    {
        await _dispatcher.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);
        // Tüm refresh token'lar revoke edildi → mevcut oturum cookie'si de temizlenir.
        RefreshCookie.Clear(Response, _secureCookie);
        return Ok(ApiResponse<object?>.Ok(null, "Parolanız güncellendi. Oturumlarınız yenilendi; lütfen tekrar giriş yapın."));
    }

    // ---- Sprint 31-MFA: TOTP ----

    [Authorize]
    [HttpPost("mfa/setup/start")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaSetupStart(CancellationToken ct)
    {
        var r = await _dispatcher.Send(new MfaSetupStartCommand(), ct);
        return Ok(ApiResponse<MfaSetupStartResponse>.Ok(new MfaSetupStartResponse(r.OtpauthUri, r.ManualKey)));
    }

    [Authorize]
    [HttpPost("mfa/setup/verify")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaSetupVerify(MfaSetupVerifyRequest body, CancellationToken ct)
    {
        var r = await _dispatcher.Send(new MfaSetupVerifyCommand(body.Code, ClientIp), ct);
        if (!string.IsNullOrEmpty(r.Auth.RefreshToken))
            RefreshCookie.Write(Response, r.Auth.RefreshToken, r.Auth.RefreshTokenExpiresAt, _secureCookie);
        return Ok(ApiResponse<MfaSetupCompleteResponse>.Ok(
            new MfaSetupCompleteResponse(AuthResponse.From(r.Auth), r.RecoveryCodes),
            "MFA etkinleştirildi. Kurtarma kodlarınızı güvenli bir yerde saklayın (bir daha gösterilmez)."));
    }

    // Anonim: login 2. faktörü; kullanıcı henüz tam access token'a sahip değil (challenge token body'de).
    [AllowAnonymous]
    [HttpPost("mfa/challenge/verify")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaChallengeVerify(MfaChallengeVerifyRequest body, CancellationToken ct)
    {
        var outcome = await _dispatcher.Send(
            new MfaChallengeVerifyCommand(body.ChallengeToken, body.Code, body.RememberDevice ?? false, ClientIp), ct);

        // Sprint 50: "bu cihazı hatırla" seçildiyse güvenilir cihaz cookie'sini yaz (httpOnly + Secure + SameSite=Strict).
        if (outcome.TrustedDeviceToken is { } trustedToken && outcome.TrustedDeviceExpiresAt is { } trustedExpires)
            TrustedDeviceCookie.Write(Response, trustedToken, trustedExpires, _secureCookie);

        return AuthOk(outcome.Auth, mfaSetupRequired: false, "Giriş başarılı.");
    }

    // Anonim: login 2. faktörü (kurtarma kodu); challenge token body'de, tam access token yok.
    [AllowAnonymous]
    [HttpPost("mfa/recovery-code/verify")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> MfaRecoveryVerify(MfaRecoveryCodeVerifyRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.Send(new MfaRecoveryCodeVerifyCommand(body.ChallengeToken, body.RecoveryCode, ClientIp), ct);
        return AuthOk(result, mfaSetupRequired: false, "Giriş başarılı.");
    }

    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpPost("mfa/reset")]
    public async Task<IActionResult> MfaReset(MfaResetRequest body, CancellationToken ct)
    {
        await _dispatcher.Send(new MfaResetCommand(body.UserId), ct);
        return Ok(ApiResponse<object?>.Ok(null, "Kullanıcının MFA'sı sıfırlandı; kullanıcı tekrar kurulum yapmalı."));
    }

    /// <summary>AuthOutcome'u HTTP yanıtına çevirir: challenge / setup-pending / normal token.</summary>
    private IActionResult OutcomeOk(AuthOutcome outcome, string message)
    {
        if (outcome.MfaChallengeRequired)
            return Ok(ApiResponse<MfaChallengeResponse>.Ok(new MfaChallengeResponse(outcome.ChallengeToken!), "MFA doğrulaması gerekli."));
        return AuthOk(outcome.Auth!, outcome.MfaSetupRequired, message);
    }

    /// <summary>Refresh token'ı cookie'ye yazar (varsa) ve gövdeyi refresh'siz döner.</summary>
    private IActionResult AuthOk(AuthResult result, bool mfaSetupRequired, string message)
    {
        if (!string.IsNullOrEmpty(result.RefreshToken))
            RefreshCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt, _secureCookie);

        return Ok(ApiResponse<AuthResponse>.Ok(AuthResponse.From(result, mfaSetupRequired), message));
    }
}
