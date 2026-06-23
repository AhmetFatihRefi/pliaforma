using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Equipments;

namespace Pliaforma.Api.Controllers;

/// <summary>Sprint 32A: Ekipman/envanter havuzu yönetimi (yalnız Owner/StudioAdmin). Enforcement 32B.</summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio/equipment")]
public sealed class EquipmentController : ApiControllerBase
{
    public EquipmentController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] bool? onlyActive, CancellationToken ct) =>
        Send(new GetEquipmentListQuery(onlyActive), ct: ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetEquipmentQuery(id), ct: ct);

    [HttpPost]
    public Task<IActionResult> Create(CreateEquipmentCommand command, CancellationToken ct) =>
        Send(command, "Ekipman oluşturuldu.", ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateEquipmentCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Ekipman güncellendi.", ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Send(new DeleteEquipmentCommand(id), "Ekipman silindi.", ct);
}
