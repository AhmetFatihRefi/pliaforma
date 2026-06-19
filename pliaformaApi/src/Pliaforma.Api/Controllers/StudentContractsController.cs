using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Contracts;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 24 — Öğrenci self-servis (SALT-OKUNUR): kendi sözleşmeleri + borç durumu. Yalnız Student rolü.
/// Hiçbir endpoint dışarıdan studentProfileId/contractId almaz; kimlik token'dan çözülür (resolve-own-profile).
/// </summary>
[Authorize(Roles = "Student")]
[Route("api/student")]
public sealed class StudentContractsController : ApiControllerBase
{
    public StudentContractsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("contracts")]
    public Task<IActionResult> MyContracts(CancellationToken ct) =>
        Send(new GetMyContractsQuery(), ct: ct);

    [HttpGet("debt")]
    public Task<IActionResult> MyDebt(CancellationToken ct) =>
        Send(new GetMyDebtQuery(), ct: ct);
}
