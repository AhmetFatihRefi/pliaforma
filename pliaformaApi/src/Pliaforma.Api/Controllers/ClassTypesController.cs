using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.ClassTypes;

namespace Pliaforma.Api.Controllers;

[Authorize]
[Route("api/class-types")]
public sealed class ClassTypesController : ApiControllerBase
{
    public ClassTypesController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] bool? onlyActive, CancellationToken ct) =>
        Send(new GetClassTypesQuery(onlyActive), ct: ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetClassTypeQuery(id), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost]
    public Task<IActionResult> Create(CreateClassTypeCommand command, CancellationToken ct) =>
        Send(command, "Pilates grubu oluşturuldu.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateClassTypeCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Pilates grubu güncellendi.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Send(new DeleteClassTypeCommand(id), "Pilates grubu silindi.", ct);
}
