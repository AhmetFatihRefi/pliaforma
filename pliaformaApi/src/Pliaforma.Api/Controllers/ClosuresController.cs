using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Closures;

namespace Pliaforma.Api.Controllers;

/// <summary>Kapalı gün / toplu seans işlemleri. Yalnızca Owner/StudioAdmin (stüdyo geneli işlem).</summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio/closures")]
public sealed class ClosuresController : ApiControllerBase
{
    public ClosuresController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetAll(CancellationToken ct) =>
        Send(new GetStudioClosuresQuery(), ct: ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetStudioClosureQuery(id), ct: ct);

    [HttpPost]
    public Task<IActionResult> Create(CreateStudioClosureCommand command, CancellationToken ct) =>
        Send(command, "Toplu işlem uygulandı.", ct);
}
