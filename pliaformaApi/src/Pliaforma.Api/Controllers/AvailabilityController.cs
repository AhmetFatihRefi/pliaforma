using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Availability;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 32C: Boşluk bulma motoru (read-only). Owner/StudioAdmin/Instructor görebilir; Student → 403; anonim → 401.
/// Seans oluşturma kesin doğrulamayı (32B guard) yapar — burası yalnız öneri/okuma.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio/availability")]
public sealed class AvailabilityController : ApiControllerBase
{
    public AvailabilityController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("class-type-slots")]
    public Task<IActionResult> ClassTypeSlots(
        [FromQuery] Guid classTypeId,
        [FromQuery] DateOnly? date,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int durationMinutes,
        [FromQuery] int requiredCapacity,
        [FromQuery] Guid? instructorId,
        [FromQuery] int? incrementMinutes,
        CancellationToken ct) =>
        Send(new FindClassTypeSlotsQuery(classTypeId, date, from, to, durationMinutes, requiredCapacity, instructorId, incrementMinutes), ct: ct);
}
