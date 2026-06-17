using Pliaforma.Domain.Entities;

namespace Pliaforma.Application.Common.Interfaces;

/// <summary>
/// Bildirimi kanalına teslim eder. Bu sürümde yalnızca InApp/Log gerçek anlamda "teslim"dir (kayıt görünür +
/// loglanır) ve true döner. Email/Sms/WhatsApp gerçek gönderim YAPMAZ (no-op) ve false döner. Somut uygulama
/// Infrastructure'dadır. Teslim başarısız olsa bile çağıran iş akışı (reminder taraması) bozulmaz.
/// </summary>
public interface INotificationChannelSender
{
    Task<bool> SendAsync(Notification notification, CancellationToken ct = default);
}
