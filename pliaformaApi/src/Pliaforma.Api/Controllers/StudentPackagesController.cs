using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Packages;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrenci paketleri (satış/iptal/düzeltme/liste) — yalnızca Owner/StudioAdmin.</summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio")]
public sealed class StudentPackagesController : ApiControllerBase
{
    public StudentPackagesController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("students/{studentId:guid}/packages")]
    public Task<IActionResult> ForStudent(Guid studentId, CancellationToken ct) =>
        Send(new GetStudentPackagesQuery(studentId), ct: ct);

    [HttpPost("students/{studentId:guid}/packages")]
    public Task<IActionResult> Sell(Guid studentId, SellStudentPackageCommand command, CancellationToken ct) =>
        Send(command with { StudentProfileId = studentId }, "Paket tanımlandı.", ct);

    [HttpGet("student-packages/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetStudentPackageQuery(id), ct: ct);

    [HttpPost("student-packages/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Send(new CancelStudentPackageCommand(id), "Paket iptal edildi.", ct);

    [HttpPost("student-packages/{id:guid}/adjust")]
    public Task<IActionResult> Adjust(Guid id, AdjustStudentPackageCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Paket bakiyesi düzeltildi.", ct);
}
