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

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetPackagePlanQuery(id), ct: ct);

    [HttpPost]
    public Task<IActionResult> Create(CreatePackagePlanCommand command, CancellationToken ct) =>
        Send(command, "Paket planı oluşturuldu.", ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdatePackagePlanCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Paket planı güncellendi.", ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Send(new DeletePackagePlanCommand(id), "Paket planı silindi.", ct);

    // ---- Fiyat versiyonları (Sprint 14) ----
    [HttpGet("{id:guid}/price-versions")]
    public Task<IActionResult> PriceVersions(Guid id, CancellationToken ct) =>
        Send(new GetPackagePlanPriceVersionsQuery(id), ct: ct);

    [HttpPost("{id:guid}/price-versions")]
    public Task<IActionResult> AddPriceVersion(Guid id, AddPackagePlanPriceVersionCommand command, CancellationToken ct) =>
        Send(command with { PlanId = id }, "Fiyat versiyonu eklendi.", ct);
}
