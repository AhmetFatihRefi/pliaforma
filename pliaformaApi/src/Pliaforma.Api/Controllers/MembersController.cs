using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Members;

namespace Pliaforma.Api.Controllers;

/// <summary>Kullanıcı tarafı üyelik aksiyonları (başvuru, davetler, kabul/ret).</summary>
[Authorize]
public sealed class MembersController : ApiControllerBase
{
    public MembersController(IDispatcher dispatcher) : base(dispatcher) { }

    // Kullanıcının bir stüdyoya başvurusu (tenant-öncesi).
    [HttpPost("apply")]
    public Task<IActionResult> Apply(ApplyToStudioCommand command, CancellationToken ct) =>
        Send(command, "Başvurunuz alındı.", ct);

    [HttpGet("my-invitations")]
    public Task<IActionResult> MyInvitations(CancellationToken ct) =>
        Send(new GetMyPendingInvitationsQuery(), ct: ct);

    [HttpPost("{id:guid}/accept")]
    public Task<IActionResult> Accept(Guid id, CancellationToken ct) =>
        Send(new AcceptMembershipCommand(id), "Üyelik onaylandı.", ct);

    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, CancellationToken ct) =>
        Send(new RejectMembershipCommand(id), "Üyelik reddedildi.", ct);
}
