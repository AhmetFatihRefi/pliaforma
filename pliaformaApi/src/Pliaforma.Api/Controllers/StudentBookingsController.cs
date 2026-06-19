using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Bookings;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Öğrenci self-service. Sprint 18b: ders programı **stüdyo tarafından yönetilir** → öğrenci kendi
/// rezervasyonunu YAPAMAZ ve İPTAL EDEMEZ (book/cancel → 410). Öğrenci yalnız kendi programını görür.
/// </summary>
[Authorize]
[Route("api/student")]
public sealed class StudentBookingsController : ApiControllerBase
{
    private const string SelfServiceClosed = "Ders programı stüdyo tarafından yönetilir.";

    public StudentBookingsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("bookings")]
    public Task<IActionResult> MyBookings(CancellationToken ct) =>
        Send(new GetMyBookingsQuery(), ct: ct);

    // Sprint 18b: öğrenci kendi adına rezervasyon yapamaz (admin-güdümlü).
    [HttpPost("class-sessions/{sessionId:guid}/book")]
    public IActionResult Book(Guid sessionId) => throw new GoneException(SelfServiceClosed);

    // Sprint 18b: öğrenci kendi rezervasyonunu iptal edemez (admin-güdümlü).
    [HttpPost("bookings/{id:guid}/cancel")]
    public IActionResult Cancel(Guid id) => throw new GoneException(SelfServiceClosed);
}
