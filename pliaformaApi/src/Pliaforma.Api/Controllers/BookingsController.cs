using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Bookings;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Controllers;

/// <summary>Stüdyo/personel tarafı rezervasyon yönetimi. Student erişemez.</summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio")]
public sealed class BookingsController : ApiControllerBase
{
    public BookingsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("bookings")]
    public Task<IActionResult> GetAll([FromQuery] Guid? classSessionId, [FromQuery] ClassBookingStatus? status, CancellationToken ct) =>
        Send(new GetStudioBookingsQuery(classSessionId, status), ct: ct);

    [HttpGet("bookings/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Send(new GetStudioBookingQuery(id), ct: ct);

    [HttpGet("class-sessions/{sessionId:guid}/bookings")]
    public Task<IActionResult> GetForSession(Guid sessionId, CancellationToken ct) =>
        Send(new GetSessionBookingsQuery(sessionId), ct: ct);

    // Vekaleten rezervasyon (öğrenci adına).
    [HttpPost("class-sessions/{sessionId:guid}/bookings")]
    public Task<IActionResult> Create(Guid sessionId, StaffCreateBookingCommand command, CancellationToken ct) =>
        Send(command with { ClassSessionId = sessionId }, "Rezervasyon oluşturuldu.", ct);

    [HttpPost("bookings/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancelBookingCommand command, CancellationToken ct) =>
        Send(command with { BookingId = id }, "Rezervasyon iptal edildi.", ct);
}
