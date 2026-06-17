using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Attendance;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Personel tarafı yoklama. Owner/StudioAdmin tüm seanslar; Instructor yalnızca kendi seansı (handler
/// doğrular). Student erişemez.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio")]
public sealed class AttendanceController : ApiControllerBase
{
    public AttendanceController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("class-sessions/{sessionId:guid}/attendance")]
    public Task<IActionResult> GetForSession(Guid sessionId, CancellationToken ct) =>
        Send(new GetSessionAttendanceQuery(sessionId), ct: ct);

    [HttpPut("class-sessions/{sessionId:guid}/attendance")]
    public Task<IActionResult> MarkBatch(Guid sessionId, MarkAttendanceCommand command, CancellationToken ct) =>
        Send(command with { SessionId = sessionId }, "Yoklama kaydedildi.", ct);

    [HttpPut("bookings/{bookingId:guid}/attendance")]
    public Task<IActionResult> MarkSingle(Guid bookingId, MarkBookingAttendanceCommand command, CancellationToken ct) =>
        Send(command with { BookingId = bookingId }, "Yoklama kaydedildi.", ct);

    [HttpGet("students/{studentId:guid}/attendance/summary")]
    public Task<IActionResult> StudentSummary(Guid studentId, [FromQuery] string? month, CancellationToken ct) =>
        Send(new GetStudentAttendanceSummaryQuery(studentId, month), ct: ct);
}
