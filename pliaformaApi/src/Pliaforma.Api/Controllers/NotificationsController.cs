using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Notifications;

namespace Pliaforma.Api.Controllers;

/// <summary>Kullanıcı kendi bildirimlerini görür/okur (rol fark etmez; yalnızca kendi kayıtları).</summary>
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    public NotificationsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> Mine(CancellationToken ct) =>
        Send(new GetMyNotificationsQuery(), ct: ct);

    [HttpGet("unread-count")]
    public Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Send(new GetUnreadCountQuery(), ct: ct);

    [HttpPost("{id:guid}/read")]
    public Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        Send(new MarkNotificationReadCommand(id), "Bildirim okundu.", ct);

    [HttpPost("read-all")]
    public Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        Send(new MarkAllNotificationsReadCommand(), "Tüm bildirimler okundu.", ct);
}
