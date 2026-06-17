using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Attendance;

namespace Pliaforma.Api.Controllers;

/// <summary>Öğrenci yalnızca KENDİ katılım geçmişini ve aylık özetini görür.</summary>
[Authorize]
[Route("api/student")]
public sealed class StudentAttendanceController : ApiControllerBase
{
    public StudentAttendanceController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("attendance")]
    public Task<IActionResult> Mine(CancellationToken ct) =>
        Send(new GetMyAttendanceQuery(), ct: ct);

    [HttpGet("attendance/summary")]
    public Task<IActionResult> Summary([FromQuery] string? month, CancellationToken ct) =>
        Send(new GetMyAttendanceSummaryQuery(month), ct: ct);
}
