using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.MakeupCredits;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Personel tarafı telafi kredisi yönetimi. Owner/StudioAdmin tüm öğrenciler; Instructor yalnızca
/// kendi oluşturduğu / kendi seansında rezervasyonu olan öğrenci. Politika güncelleme Owner/StudioAdmin.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio")]
public sealed class MakeupCreditsController : ApiControllerBase
{
    public MakeupCreditsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("students/{studentId:guid}/makeup-credits")]
    public Task<IActionResult> GetForStudent(Guid studentId, CancellationToken ct) =>
        Send(new GetStudentMakeupCreditsQuery(studentId), ct: ct);

    // Sprint 20 → 28C-2: telafi dersine eklenebilir LINE'lar (kullanılabilir line hak > 0). Opsiyonel level filtre.
    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("makeup-eligible-students")]
    public Task<IActionResult> EligibleStudents([FromQuery] Domain.Enums.StudentLevel? level, CancellationToken ct) =>
        Send(new GetMakeupEligibleStudentsQuery(level), ct: ct);

    // Sprint 26 ek (v1.1.1): telafi kredisi verme/geri alma yalnız Owner/StudioAdmin (Instructor kredi veremez).
    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("students/{studentId:guid}/makeup-credits/grant")]
    public Task<IActionResult> Grant(Guid studentId, GrantMakeupCreditCommand command, CancellationToken ct) =>
        Send(command with { StudentProfileId = studentId }, "Telafi kredisi verildi.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("students/{studentId:guid}/makeup-credits/revoke")]
    public Task<IActionResult> Revoke(Guid studentId, RevokeMakeupCreditCommand command, CancellationToken ct) =>
        Send(command with { StudentProfileId = studentId }, "Telafi kredisi geri alındı.", ct);

    [HttpGet("makeup-policy")]
    public Task<IActionResult> GetPolicy(CancellationToken ct) =>
        Send(new GetMakeupPolicyQuery(), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPut("makeup-policy")]
    public Task<IActionResult> UpdatePolicy(UpdateMakeupPolicyCommand command, CancellationToken ct) =>
        Send(command, "Telafi politikası güncellendi.", ct);
}
