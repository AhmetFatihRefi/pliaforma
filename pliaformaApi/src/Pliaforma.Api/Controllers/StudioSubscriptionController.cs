using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Subscriptions;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Stüdyo-tarafı abonelik (yalnız Owner): kendi aboneliğini görür + dönem sonunda iptal eder. License-gated
/// DEĞİL (lisanssızken bile görüntüleme/iptal açık). Cross-tenant yok — daima token'daki aktif stüdyo.
/// </summary>
[Authorize(Roles = "Owner")]
[Route("api/studio/subscription")]
public sealed class StudioSubscriptionController : ApiControllerBase
{
    public StudioSubscriptionController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> Mine(CancellationToken ct) =>
        Send(new GetMyStudioSubscriptionQuery(), ct: ct);

    [HttpPost("cancel")]
    public Task<IActionResult> Cancel(CancellationToken ct) =>
        Send(new CancelMyStudioSubscriptionCommand(), ct: ct);
}
