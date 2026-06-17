using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Dashboards;

namespace Pliaforma.Api.Controllers;

/// <summary>Hoca kendi aylık dashboard'unu görür (başka hocanınkini göremez).</summary>
[Authorize(Roles = "Instructor")]
[Route("api/instructor")]
public sealed class InstructorDashboardController : ApiControllerBase
{
    public InstructorDashboardController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("dashboard")]
    public Task<IActionResult> Mine([FromQuery] string? month, CancellationToken ct) =>
        Send(new GetMyInstructorDashboardQuery(month), ct: ct);
}
