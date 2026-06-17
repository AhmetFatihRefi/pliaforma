using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Dashboards;
using Pliaforma.Application.Features.Reports;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Controllers;

/// <summary>Stüdyo dashboard'ları ve raporlar. Finans/özetler Owner/StudioAdmin; öğrenci dashboard'u staff.</summary>
[Authorize]
[Route("api/studio")]
public sealed class DashboardController : ApiControllerBase
{
    public DashboardController(IDispatcher dispatcher) : base(dispatcher) { }

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("dashboard")]
    public Task<IActionResult> Studio([FromQuery] string? month, CancellationToken ct) =>
        Send(new GetStudioDashboardQuery(month), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("instructors/{instructorId:guid}/dashboard")]
    public Task<IActionResult> Instructor(Guid instructorId, [FromQuery] string? month, CancellationToken ct) =>
        Send(new GetInstructorDashboardQuery(instructorId, month), ct: ct);

    // Öğrenci dashboard'u: Owner/StudioAdmin + Instructor (handler erişim + finans gizliliğini uygular).
    [Authorize(Roles = "Owner,StudioAdmin,Instructor")]
    [HttpGet("students/{studentId:guid}/dashboard")]
    public Task<IActionResult> StudentForStaff(Guid studentId, [FromQuery] string? month, CancellationToken ct) =>
        Send(new GetStaffStudentDashboardQuery(studentId, month), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("reports/payments")]
    public Task<IActionResult> PaymentsReport([FromQuery] string? month, [FromQuery] PaymentStatus? status, CancellationToken ct) =>
        Send(new GetPaymentsReportQuery(month, status), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("reports/instructors")]
    public Task<IActionResult> InstructorsReport([FromQuery] string? month, CancellationToken ct) =>
        Send(new GetInstructorsReportQuery(month), ct: ct);
}
