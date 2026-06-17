using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Dashboards;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrenci kendi aylık dashboard'unu görür (başkasınınkini göremez).</summary>
[Authorize]
[Route("api/student")]
public sealed class StudentDashboardController : ApiControllerBase
{
    public StudentDashboardController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("dashboard")]
    public Task<IActionResult> Mine([FromQuery] string? month, CancellationToken ct) =>
        Send(new GetMyStudentDashboardQuery(month), ct: ct);
}
