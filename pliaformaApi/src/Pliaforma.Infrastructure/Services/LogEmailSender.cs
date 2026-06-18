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
        _logger.LogInformation("[EMAIL:Log] EmailVerification → {To} | link: {Link}", toEmail, msg.Link);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default)
    {
        var msg = EmailComposer.PasswordReset(_settings, fullName, rawToken);
        _logger.LogInformation("[EMAIL:Log] PasswordReset → {To} | link: {Link}", toEmail, msg.Link);
        return Task.CompletedTask;
    }
}

/// <summary>Bilinçli no-op gönderici (Disabled modu). Hiçbir gönderim/loglama yapmaz.</summary>
public sealed class NullEmailSender : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string fullName, string rawToken, CancellationToken ct = default) => Task.CompletedTask;
}
