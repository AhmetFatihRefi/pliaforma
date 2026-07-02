using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Bookings;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Api.Controllers;

/// <summary>
/// Stüdyo/personel tarafı rezervasyon yönetimi. GET (seans bağlamı) Instructor'a açık (kendi seansının
/// rezervasyonlarını yoklama için görür); ancak rezervasyon EKLEME/İPTAL v1.1.2 (role hardening) ile yalnız
/// Owner/StudioAdmin (öğrenci atama stüdyo yetkilisinin işi). Student erişemez.
/// </summary>
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

    // Sprint 29B: aday öğrenci listeleri (öğrenci ekleme operasyonunun parçası → yalnız Owner/StudioAdmin).
    // Yanlış session kind → 400 (regular-candidates yalnız Regular; makeup-candidates yalnız Makeup).
    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("class-sessions/{sessionId:guid}/regular-candidates")]
    public Task<IActionResult> RegularCandidates(Guid sessionId, [FromQuery] StudentLevel? level, CancellationToken ct) =>
        Send(new GetRegularBookingCandidatesQuery(sessionId, level), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet("class-sessions/{sessionId:guid}/makeup-candidates")]
    public Task<IActionResult> MakeupCandidates(Guid sessionId, [FromQuery] StudentLevel? level, CancellationToken ct) =>
        Send(new GetMakeupBookingCandidatesQuery(sessionId, level), ct: ct);

    // Vekaleten rezervasyon (öğrenci adına). Owner/StudioAdmin her zaman; Sprint 46: Instructor YALNIZ pilot mod
    // açıkken (handler 403 verir) + yalnız KENDİ seansına. Kapalıyken instructor mevcut gibi 403 alır.
    [Authorize(Roles = "Owner,StudioAdmin,Instructor")]
    [HttpPost("class-sessions/{sessionId:guid}/bookings")]
    public Task<IActionResult> Create(Guid sessionId, StaffCreateBookingCommand command, CancellationToken ct) =>
        Send(command with { ClassSessionId = sessionId }, "Rezervasyon oluşturuldu.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("bookings/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancelBookingCommand command, CancellationToken ct) =>
        Send(command with { BookingId = id }, "Rezervasyon iptal edildi.", ct);
}
