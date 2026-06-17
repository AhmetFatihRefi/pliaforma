using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Notifications;

namespace Pliaforma.Api.Controllers;

/// <summary>Stüdyo hatırlatma taramasını elle çalıştırma (Owner/StudioAdmin) — yalnızca aktif stüdyo için.</summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio/notifications")]
public sealed class StudioNotificationsController : ApiControllerBase
{
    public StudioNotificationsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpPost("run-reminders")]
    public Task<IActionResult> RunReminders(CancellationToken ct) =>
        Send(new RunStudioRemindersCommand(), "Hatırlatma taraması çalıştı.", ct);
}
