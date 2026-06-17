using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Scheduling;

namespace Pliaforma.Api.Controllers;

/// <summary>Hoca müsaitlik yönetimi. Student erişemez (Owner/StudioAdmin/Instructor).</summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio")]
public sealed class InstructorAvailabilitiesController : ApiControllerBase
{
    public InstructorAvailabilitiesController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("instructor-availabilities")]
    public Task<IActionResult> GetAll([FromQuery] Guid? instructorProfileId, CancellationToken ct) =>
        Send(new GetAvailabilitiesQuery(instructorProfileId), ct: ct);

    [HttpGet("instructors/{instructorId:guid}/availabilities")]
    public Task<IActionResult> GetForInstructor(Guid instructorId, CancellationToken ct) =>
        Send(new GetAvailabilitiesQuery(instructorId), ct: ct);

    [HttpPost("instructors/{instructorId:guid}/availabilities")]
    public Task<IActionResult> Create(Guid instructorId, CreateAvailabilityCommand command, CancellationToken ct) =>
        Send(command with { InstructorProfileId = instructorId }, "Müsaitlik eklendi.", ct);

    [HttpPut("instructor-availabilities/{id:guid}")]
    public Task<IActionResult> Update(Guid id, UpdateAvailabilityCommand command, CancellationToken ct) =>
        Send(command with { Id = id }, "Müsaitlik güncellendi.", ct);

    [HttpDelete("instructor-availabilities/{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Send(new DeleteAvailabilityCommand(id), "Müsaitlik silindi.", ct);
}
