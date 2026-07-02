using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Students;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Sprint 46 — Pilot Simple People Mode. Misafir (ad-soyad) öğrenci oluşturma + hoca için minimal öğrenci
/// listesi. Owner/StudioAdmin her zaman; Instructor YALNIZ pilot mod açıkken (handler 403 verir). Rota
/// <c>api/studio/students</c> altında kalır (URL değişmez); tam-DTO liste/detay/update StudentsController'da
/// Owner/StudioAdmin'e kilitli kalır (v1.1.2 privacy). Minimal DTO hassas PII/sağlık/finans döndürmez.
/// </summary>
[Authorize(Roles = "Owner,StudioAdmin,Instructor")]
[Route("api/studio/students")]
public sealed class StudioGuestStudentsController : ApiControllerBase
{
    public StudioGuestStudentsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpPost("guest")]
    public Task<IActionResult> CreateGuest(CreateGuestStudentCommand command, CancellationToken ct) =>
        Send(command, "Misafir öğrenci oluşturuldu.", ct);

    [HttpGet("minimal")]
    public Task<IActionResult> Minimal([FromQuery] string? search, CancellationToken ct) =>
        Send(new GetStudentsMinimalQuery(search), ct: ct);
}
