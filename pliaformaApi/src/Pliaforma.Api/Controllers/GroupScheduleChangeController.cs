using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Contracts;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 22: köklü grup düzeni değişikliği (öğrencileri tek tek seçip yeni gün/saat düzenine taşıma).
/// Yalnız Owner/StudioAdmin. Önce candidates → preview → apply (atomik, telafisiz).
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin")]
[Route("api/studio/group-schedule-change")]
public sealed class GroupScheduleChangeController : ApiControllerBase
{
    public GroupScheduleChangeController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("candidates")]
    public Task<IActionResult> Candidates(
        [FromQuery] Guid instructorProfileId, [FromQuery] Guid classTypeId, [FromQuery] DateOnly effectiveDate,
        [FromQuery] Domain.Enums.StudentLevel? level, CancellationToken ct) =>
        Send(new GetGroupChangeCandidatesQuery(instructorProfileId, classTypeId, effectiveDate, level), ct: ct);

    [HttpPost("preview")]
    public Task<IActionResult> Preview(PreviewGroupScheduleChangeQuery query, CancellationToken ct) =>
        Send(query, ct: ct);

    [HttpPost("apply")]
    public Task<IActionResult> Apply(ApplyGroupScheduleChangeCommand command, CancellationToken ct) =>
        Send(command, "Grup düzeni değişikliği uygulandı.", ct);
}
