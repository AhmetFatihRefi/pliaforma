using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Payments;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Controllers;

/// <summary>Ödeme yönetimi — yalnızca Owner/StudioAdmin (Instructor finansal tutarları görmez).</summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio")]
public sealed class PaymentsController : ApiControllerBase
{
    public PaymentsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("payments")]
    public Task<IActionResult> GetAll([FromQuery] Guid? studentProfileId, [FromQuery] PaymentStatus? status, CancellationToken ct) =>
        Send(new GetPaymentsQuery(studentProfileId, status), ct: ct);

    [HttpGet("students/{studentId:guid}/payments")]
    public Task<IActionResult> ForStudent(Guid studentId, CancellationToken ct) =>
        Send(new GetStudentPaymentsQuery(studentId), ct: ct);

    [HttpPost("students/{studentId:guid}/payments")]
    public Task<IActionResult> Create(Guid studentId, CreatePaymentCommand command, CancellationToken ct) =>
        Send(command with { StudentProfileId = studentId }, "Ödeme kaydı oluşturuldu.", ct);

    [HttpPut("payments/{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdatePaymentCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Ödeme güncellendi.", ct);

    [HttpPost("payments/{id:guid}/mark-paid")]
    public Task<IActionResult> MarkPaid(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MarkPaymentPaidCommand? command, CancellationToken ct) =>
        Send((command ?? new MarkPaymentPaidCommand(id, null)) with { Id = id }, "Ödeme ödendi olarak işaretlendi.", ct);

    [HttpPost("payments/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Send(new CancelPaymentCommand(id), "Ödeme iptal edildi.", ct);
}
