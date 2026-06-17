using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Bookings;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrenci self-service rezervasyon (kendi aktif profili ile).</summary>
[Authorize]
[Route("api/student")]
public sealed class StudentBookingsController : ApiControllerBase
{
    public StudentBookingsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("bookings")]
    public Task<IActionResult> MyBookings(CancellationToken ct) =>
        Send(new GetMyBookingsQuery(), ct: ct);

    [HttpGet("class-sessions")]
    public Task<IActionResult> BookableSessions([FromQuery] GetStudentSessionsQuery query, CancellationToken ct) =>
        Send(query, ct: ct);

    [HttpPost("class-sessions/{sessionId:guid}/book")]
    public Task<IActionResult> Book(Guid sessionId, CancellationToken ct) =>
        Send(new StudentBookCommand(sessionId), "Rezervasyon yapıldı.", ct);

    [HttpPost("bookings/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Send(new CancelBookingCommand(id), "Rezervasyon iptal edildi.", ct);
}
