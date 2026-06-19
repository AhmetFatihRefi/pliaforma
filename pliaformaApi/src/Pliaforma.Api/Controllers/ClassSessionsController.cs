using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Attendance;
using Pliaforma.Application.Features.Scheduling;

namespace Pliaforma.Api.Controllers;

/// <summary>Ders seansı yönetimi. Student erişemez (Owner/StudioAdmin/Instructor).</summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio/class-sessions")]
public sealed class ClassSessionsController : ApiControllerBase
{
    public ClassSessionsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] GetClassSessionsQuery query, CancellationToken ct) =>
        Send(query, ct: ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetClassSessionQuery(id), ct: ct);

    [HttpPost]
    public Task<IActionResult> Create(CreateClassSessionCommand command, CancellationToken ct) =>
        Send(command, "Ders seansı oluşturuldu.", ct);

    [HttpPost("bulk-generate")]
    public Task<IActionResult> BulkGenerate(BulkGenerateClassSessionsCommand command, CancellationToken ct) =>
        Send(command, "Slotlar üretildi.", ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateClassSessionCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Ders seansı güncellendi.", ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelClassSessionCommand? command, CancellationToken ct) =>
        Send((command ?? new CancelClassSessionCommand(id)) with { Id = id }, "Ders seansı iptal edildi.", ct);

    [HttpPost("{id:guid}/reschedule")]
    public Task<IActionResult> Reschedule(Guid id, RescheduleClassSessionCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Ders seansı ertelendi.", ct);

    // ---- Sprint 19: ders onayı + fiilî hoca ----
    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct) =>
        Send(new ConfirmSessionCommand(id), "Ders onaylandı.", ct);

    [HttpPost("confirm-today")]
    public Task<IActionResult> ConfirmToday(CancellationToken ct) =>
        Send(new ConfirmTodaySessionsCommand(), "Bugünün dersleri onaylandı.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("{id:guid}/actual-instructor")]
    public Task<IActionResult> SetActualInstructor(Guid id, SetActualInstructorCommand command, CancellationToken ct) =>
        Send(command with { SessionId = id }, "Fiilî hoca güncellendi.", ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Send(new DeleteClassSessionCommand(id), "Ders seansı silindi.", ct);
}
