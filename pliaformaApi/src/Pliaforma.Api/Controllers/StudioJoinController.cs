using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Members;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Stüdyo tarafı self-service katılım (Sprint 15): bekleyen katılım istekleri + katılım kodu yönetimi.
/// Yalnızca Owner/StudioAdmin.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio")]
public sealed class StudioJoinController : ApiControllerBase
{
    public StudioJoinController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("join-requests")]
    public Task<IActionResult> JoinRequests(CancellationToken ct) =>
        Send(new GetJoinRequestsQuery(), ct: ct);

    [HttpGet("join-code")]
    public Task<IActionResult> GetJoinCode(CancellationToken ct) =>
        Send(new GetJoinCodeQuery(), ct: ct);

    [HttpPost("join-code/regenerate")]
    public Task<IActionResult> RegenerateJoinCode(CancellationToken ct) =>
        Send(new RegenerateJoinCodeCommand(), "Katılım kodu yenilendi.", ct);
}
