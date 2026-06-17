using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Packages;

namespace Pliaforma.Api.Controllers;

/// <summary>Paket planı (şablon) yönetimi — yalnızca Owner/StudioAdmin (finans).</summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio/package-plans")]
public sealed class PackagePlansController : ApiControllerBase
{
    public PackagePlansController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] bool onlyActive, CancellationToken ct) =>
        Send(new GetPackagePlansQuery(onlyActive), ct: ct);

    [HttpPost]
    public Task<IActionResult> Create(CreatePackagePlanCommand command, CancellationToken ct) =>
        Send(command, "Paket planı oluşturuldu.", ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdatePackagePlanCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Paket planı güncellendi.", ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Send(new DeletePackagePlanCommand(id), "Paket planı silindi.", ct);
}
