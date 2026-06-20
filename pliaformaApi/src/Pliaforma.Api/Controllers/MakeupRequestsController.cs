using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.MakeupRequests;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Telafi dersi talepleri (Sprint 27). Instructor + Owner/StudioAdmin talep oluşturur/listeler/withdraw eder
/// (Instructor yalnız KENDİ profili + kendi aktif sözleşmeli öğrencisi — handler zorlar). Approve/Reject/edit-approve
/// yalnız Owner/StudioAdmin (method-level role → controller rolüyle AND → Instructor 403). Student erişemez.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio")]
public sealed class MakeupRequestsController : ApiControllerBase
{
    public MakeupRequestsController(IDispatcher dispatcher) : base(dispatcher) { }

    // Talep açılabilecek öğrenciler (kullanılabilir telafi hakkı > 0). Instructor → kendi sözleşmeli öğrencileri.
    [HttpGet("makeup-requests/eligible-students")]
    public Task<IActionResult> EligibleStudents(CancellationToken ct) =>
        Send(new GetMakeupRequestEligibleStudentsQuery(), ct: ct);

    [HttpGet("makeup-requests")]
    public Task<IActionResult> GetAll([FromQuery] MakeupRequestStatus? status, CancellationToken ct) =>
        Send(new GetMakeupRequestsQuery(status), ct: ct);

    [HttpGet("makeup-requests/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetMakeupRequestQuery(id), ct: ct);

    [HttpPost("makeup-requests")]
    public Task<IActionResult> Create(CreateMakeupRequestCommand command, CancellationToken ct) =>
        Send(command, "Telafi talebi oluşturuldu.", ct);

    [HttpPost("makeup-requests/{id:guid}/withdraw")]
    public Task<IActionResult> Withdraw(Guid id, CancellationToken ct) =>
        Send(new WithdrawMakeupRequestCommand(id), "Telafi talebi geri çekildi.", ct);

    // Onaylama/red yalnız Owner/StudioAdmin (Instructor görmez/yapamaz).
    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("makeup-requests/{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, ApproveMakeupRequestCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Telafi talebi onaylandı.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("makeup-requests/{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, RejectMakeupRequestCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Telafi talebi reddedildi.", ct);
}
