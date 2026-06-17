using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.MakeupCredits;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrenci kendi telafi kredisi bakiyesini ve geçmişini görür.</summary>
[Authorize]
[Route("api/student")]
public sealed class StudentMakeupCreditsController : ApiControllerBase
{
    public StudentMakeupCreditsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("makeup-credits")]
    public Task<IActionResult> Mine(CancellationToken ct) =>
        Send(new GetMyMakeupCreditsQuery(), ct: ct);
}
