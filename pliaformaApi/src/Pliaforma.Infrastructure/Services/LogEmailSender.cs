using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Infrastructure.Settings;

namespace Pliaforma.Infrastructure.Services;

/// <summary>
/// Dev/test e-posta gönderici (Sprint 16): gerçek gönderim YAPMAZ; doğrulama/sıfırlama linkini **loglar**.
/// Smoke/E2E ve yerel geliştirme bu modu kullanır. Prod'da SmtpEmailSender devreye girer.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(IOptions<EmailSettings> settings, ILogger<LogEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default)
    {
        var msg = EmailComposer.Verification(_settings, fullName, rawToken);
        // Information: token/PII içermez (varsayılan/üretim-benzeri seviyede sızıntı olmaz).
        _logger.LogInformation("[EMAIL:Log] EmailVerification gönderildi (alıcı/link gizli; ham token Debug seviyesinde).");
        // Debug: tam link (ham token gömülü) — YALNIZ yerel geliştirme. Prod'da Log modu DI guard ile yasak (ConfigureEmail).
        _logger.LogDebug("[EMAIL:Log] EmailVerification → {To} | link: {Link}", toEmail, msg.Link);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default)
    {
        var msg = EmailComposer.PasswordReset(_settings, fullName, rawToken);
        // Information: token/PII içermez.
        _logger.LogInformation("[EMAIL:Log] PasswordReset gönderildi (alıcı/link gizli; ham token Debug seviyesinde).");
        // Debug: tam link (ham token gömülü) — YALNIZ yerel geliştirme.
        _logger.LogDebug("[EMAIL:Log] PasswordReset → {To} | link: {Link}", toEmail, msg.Link);
        return Task.CompletedTask;
    }

    // Sprint 52: iletişim formu — dev/test'te gerçek gönderim yok. Information PII içermez;
    // içerik yalnız Debug seviyesinde (yerel geliştirme).
    public Task SendContactMessageAsync(ContactMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL:Log] ContactMessage alındı (içerik Debug seviyesinde; gerçek gönderim yok).");
        _logger.LogDebug("[EMAIL:Log] ContactMessage → studio: {Studio}, from: {From}", message.StudioName, message.Email ?? message.Phone);
        return Task.CompletedTask;
    }
}

/// <summary>Bilinçli no-op gönderici (Disabled modu). Hiçbir gönderim/loglama yapmaz.</summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendContactMessageAsync(ContactMessage message, CancellationToken ct = default) => Task.CompletedTask;
}
