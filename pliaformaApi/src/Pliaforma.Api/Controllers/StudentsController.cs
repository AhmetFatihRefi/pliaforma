using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Students;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Stüdyo öğrenci yönetimi — yalnız Owner/StudioAdmin. v1.1.2 (privacy/role hardening): GET (liste/arama/by-id)
/// de Instructor'a KAPALI (DTO hassas PII/HealthNotes döndürür + scope "creator" yanlış eksende). Instructor
/// kendi seansındaki öğrencileri yalnız SEANS-bağlamlı uçlardan (bookings/attendance) görür. Student hiç erişemez.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio/students")]
public sealed class StudentsController : ApiControllerBase
{
    public StudentsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] Domain.Enums.StudentLevel? level, CancellationToken ct) =>
        Send(new GetStudentsQuery(Level: level), ct: ct);

    [HttpGet("search")]
    public Task<IActionResult> Search([FromQuery] string? query, [FromQuery] Domain.Enums.StudentLevel? level, CancellationToken ct) =>
        Send(new GetStudentsQuery(query, level), ct: ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetStudentQuery(id), ct: ct);

    [HttpPost("guest")]
    public Task<IActionResult> CreateGuest(CreateGuestStudentCommand command, CancellationToken ct) =>
        Send(command, "Misafir öğrenci oluşturuldu.", ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateStudentCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Öğrenci güncellendi.", ct);

    [HttpPost("{id:guid}/convert-to-user")]
    public Task<IActionResult> Convert(Guid id, ConvertStudentToUserCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Öğrenci kullanıcıya bağlandı.", ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        Send(new DeactivateStudentCommand(id), "Öğrenci pasifleştirildi.", ct);

    [HttpPost("{id:guid}/reactivate")]
    public Task<IActionResult> Reactivate(Guid id, CancellationToken ct) =>
        Send(new ReactivateStudentCommand(id), "Öğrenci yeniden aktifleştirildi.", ct);
}
