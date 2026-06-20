using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Contracts;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Öğrenci sözleşmesi yönetimi + sözleşmeden program üretimi (Sprint 18a). Owner/StudioAdmin.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio")]
public sealed class ContractsController : ApiControllerBase
{
    public ContractsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("students/{studentId:guid}/contracts")]
    public Task<IActionResult> ListForStudent(Guid studentId, CancellationToken ct) =>
        Send(new GetStudentContractsQuery(studentId), ct: ct);

    [HttpPost("students/{studentId:guid}/contracts")]
    public Task<IActionResult> Create(Guid studentId, CreateStudentContractCommand command, CancellationToken ct) =>
        Send(command with { StudentProfileId = studentId }, "Sözleşme oluşturuldu.", ct);

    [HttpGet("contracts/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetStudentContractQuery(id), ct: ct);

    [HttpPost("contracts/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Send(new CancelStudentContractCommand(id), "Sözleşme iptal edildi.", ct);

    [HttpPost("contracts/{id:guid}/schedule/preview")]
    public Task<IActionResult> Preview(Guid id, PreviewContractScheduleQuery body, CancellationToken ct) =>
        Send(body with { ContractId = id }, ct: ct);

    [HttpPost("contracts/{id:guid}/schedule/generate")]
    public Task<IActionResult> Generate(Guid id, GenerateContractScheduleCommand body, CancellationToken ct) =>
        Send(body with { ContractId = id }, "Program oluşturuldu.", ct);

    // ---- Sprint 23: ek işlem (amendment) + borç ----

    [HttpPost("contracts/{id:guid}/amendments")]
    public Task<IActionResult> AddAmendment(Guid id, AddContractAmendmentCommand body, CancellationToken ct) =>
        Send(body with { ContractId = id }, "Ek işlem kaydedildi.", ct);

    [HttpGet("contracts/{id:guid}/amendments")]
    public Task<IActionResult> ListAmendments(Guid id, CancellationToken ct) =>
        Send(new GetContractAmendmentsQuery(id), ct: ct);

    [HttpGet("students/{studentId:guid}/debt")]
    public Task<IActionResult> StudentDebt(Guid studentId, CancellationToken ct) =>
        Send(new GetStudentDebtQuery(studentId), ct: ct);

    // Sprint 28A: öğrencinin sözleşme satırları (read-only).
    [HttpGet("students/{studentId:guid}/contract-lines")]
    public Task<IActionResult> ContractLines(Guid studentId, CancellationToken ct) =>
        Send(new GetStudentContractLinesQuery(studentId), ct: ct);

    // Sprint 28B: yeni line (ek alım). HeaderId verilmezse aktif header'a; yoksa yeni header (otomatik).
    [HttpPost("students/{studentId:guid}/contract-lines")]
    public Task<IActionResult> AddContractLine(Guid studentId, CreateStudentContractLineCommand command, CancellationToken ct) =>
        Send(command with { StudentProfileId = studentId }, "Sözleşme satırı eklendi.", ct);

    // Sprint 28B: line edit — yalnız son ödeme tarihi + not.
    [HttpPut("contract-lines/{lineId:guid}")]
    public Task<IActionResult> UpdateContractLine(Guid lineId, UpdateContractLineCommand command, CancellationToken ct) =>
        Send(command with { LineId = lineId }, "Sözleşme satırı güncellendi.", ct);

    // Sprint 28B: güvenli line iptal (bağlı ödeme/ders/telafi varsa 409).
    [HttpPost("contract-lines/{lineId:guid}/cancel")]
    public Task<IActionResult> CancelContractLine(Guid lineId, CancellationToken ct) =>
        Send(new CancelContractLineCommand(lineId), "Sözleşme satırı iptal edildi.", ct);
}
