using Microsoft.Extensions.Logging;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Infrastructure.Services;

/// <summary>
/// Bu sürümün tek bildirim kanalı uygulaması. InApp/Log → kayıt zaten uygulamada görünür; burada yalnızca
/// loglanır ve teslim başarılı sayılır (true). Email/Sms/WhatsApp → **gerçek gönderim YOK** (no-op): loglanır
/// ve false döner (Status=Failed). Dış servis ÇAĞRILMAZ. İleride her kanal için ayrı sender eklenebilir.
/// </summary>
public sealed class LoggingNotificationSender : INotificationChannelSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) => _logger = logger;

    public Task<bool> SendAsync(Notification notification, CancellationToken ct = default)
    {
        switch (notification.Channel)
        {
            case NotificationChannel.InApp:
            case NotificationChannel.Log:
                _logger.LogInformation(
                    "Bildirim teslim (InApp/Log) studio={StudioId} user={UserId} type={Type}: {Title}",
                    notification.StudioId, notification.RecipientUserId, notification.Type, notification.Title);
                return Task.FromResult(true);

            default:
                // Email/Sms/WhatsApp bu sürümde uygulanmadı — dış servis çağrılmaz.
                _logger.LogInformation(
                    "Bildirim kanalı {Channel} bu sürümde uygulanmadı (no-op), atlandı: {DedupKey}",
                    notification.Channel, notification.DeduplicationKey);
                return Task.FromResult(false);
        }
    }
}
