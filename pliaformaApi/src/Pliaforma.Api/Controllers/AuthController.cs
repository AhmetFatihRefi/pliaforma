using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pliaforma.Api.Auth;
using Pliaforma.Api.RateLimiting;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Models;
using Pliaforma.Application.Features.Auth;

namespace Pliaforma.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly bool _secureCookie;

    public AuthController(IDispatcher dispatcher, IHostEnvironment env)
    {
        _dispatcher = dispatcher;
        // Geliştirmede HTTP üzerinden çalışıldığı için Secure=false; üretimde HTTPS zorunlu.
        _secureCookie = !env.IsDevelopment();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register-studio")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> RegisterStudio(RegisterStudioRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.Send(
            new RegisterStudioCommand(body.StudioName, body.OwnerFullName, body.Email, body.Password, body.Phone, ClientIp), ct);
        return AuthOk(result, "Stüdyo oluşturuldu.");
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Register(RegisterRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.Send(
            new RegisterCommand(body.FullName, body.Email, body.Password, body.Phone, ClientIp), ct);
        return AuthOk(result, "Hesap oluşturuldu.");
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login(LoginRequest body, CancellationToken ct)
    {
        var result = await _dispatcher.Send(new LoginCommand(body.Email, body.Password, ClientIp), ct);
        return AuthOk(result, "Giriş başarılı.");
    }

    [Authorize]
    [HttpPost("switch-studio")]
    public async Task<IActionResult> SwitchStudio(SwitchStudioRequest body, CancellationToken ct)
    {
        // Access-only: refresh token'a dokunulmaz, cookie aynı kalır.
        var result = await _dispatcher.Send(new SwitchStudioCommand(body.StudioId), ct);
        return Ok(ApiResponse<AuthResponse>.Ok(AuthResponse.From(result), "Stüdyo değiştirildi."));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Refresh(RefreshRequest body, CancellationToken ct)
    {
        var raw = RefreshCookie.Read(Request)
            ?? throw new Domain.Exceptions.UnauthorizedException("Oturum bulunamadı. Lütfen tekrar giriş yapın.");

        var result = await _dispatcher.Send(new RefreshTokenCommand(raw, body.StudioId, ClientIp), ct);
        return AuthOk(result, "Oturum yenilendi.");
    }

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

    /// <summary>Refresh token'ı cookie'ye yazar (varsa) ve gövdeyi refresh'siz döner.</summary>
    private IActionResult AuthOk(AuthResult result, string message)
    {
        if (!string.IsNullOrEmpty(result.RefreshToken))
            RefreshCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt, _secureCookie);

        return Ok(ApiResponse<AuthResponse>.Ok(AuthResponse.From(result), message));
    }
}
