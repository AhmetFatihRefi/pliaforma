using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.MakeupRequests;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Öğrenci kendi telafi taleplerini SALT görür (Sprint 27 — programda "Onay bekleyen telafi talebi" / onaylanmış
/// telafi dersi). Öğrenci işlem yapamaz (oluşturma/onay/red/withdraw yok).
/// </summary>
[Authorize]
[Route("api/student")]
public sealed class StudentMakeupRequestsController : ApiControllerBase
{
    public StudentMakeupRequestsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("makeup-requests")]
    public Task<IActionResult> Mine(CancellationToken ct) =>
        Send(new GetMyMakeupRequestsQuery(), ct: ct);
}
