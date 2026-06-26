using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Studios;

namespace Pliaforma.Api.Controllers;

[Authorize]
[Route("api/studio")]
public sealed class StudioController : ApiControllerBase
{
    public StudioController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetMine(CancellationToken ct) =>
        Send(new GetMyStudioQuery(), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPut]
    public Task<IActionResult> Update(UpdateStudioCommand command, CancellationToken ct) =>
        Send(command, "Stüdyo güncellendi.", ct);

    // ---- Sprint 37: Çalışma saatleri. Okuma tüm rollere (grid/availability); yazma Owner/StudioAdmin. ----
    [HttpGet("working-hours")]
    public Task<IActionResult> GetWorkingHours(CancellationToken ct) =>
        Send(new GetStudioWorkingHoursQuery(), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPut("working-hours")]
    public Task<IActionResult> UpdateWorkingHours(UpdateStudioWorkingHoursCommand command, CancellationToken ct) =>
        Send(command, "Çalışma saatleri güncellendi.", ct);
}
