using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Packages;
using Pliaforma.Application.Features.Payments;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrenci yalnızca KENDİ paket ve ödeme bilgisini görür.</summary>
[Authorize]
[Route("api/student")]
public sealed class StudentFinanceController : ApiControllerBase
{
    public StudentFinanceController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("packages")]
    public Task<IActionResult> MyPackages(CancellationToken ct) =>
        Send(new GetMyPackagesQuery(), ct: ct);

    [HttpGet("payments")]
    public Task<IActionResult> MyPayments(CancellationToken ct) =>
        Send(new GetMyPaymentsQuery(), ct: ct);
}
