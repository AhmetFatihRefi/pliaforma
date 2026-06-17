using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Students;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrencinin kendi profili (self-service).</summary>
[Authorize]
[Route("api/student")]
public sealed class StudentSelfController : ApiControllerBase
{
    public StudentSelfController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct) =>
        Send(new GetMyStudentProfileQuery(), ct: ct);
}
