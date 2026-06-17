using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Members;

namespace Pliaforma.Api.Controllers;

/// <summary>Stüdyo yöneticisi tarafı üyelik yönetimi (davet, rol, çıkarma).</summary>
[Authorize]
[Route("api/studio/memberships")]
public sealed class StudioMembershipsController : ApiControllerBase
{
    public StudioMembershipsController(IDispatcher dispatcher) : base(dispatcher) { }

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpGet]
    public Task<IActionResult> GetAll([FromQuery] bool includeInactive, CancellationToken ct) =>
        Send(new GetMembersQuery(includeInactive), ct: ct);

    // StudioAdmin yalnızca Instructor davet edebilir; StudioAdmin daveti handler'da Owner ile sınırlı.
    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("invite")]
    public Task<IActionResult> Invite(InviteMemberCommand command, CancellationToken ct) =>
        Send(command, "Davet gönderildi.", ct);

    // Rol değiştirme yalnızca Owner.
    [Authorize(Roles = "Owner")]
    [HttpPatch("{id:guid}/role")]
    public Task<IActionResult> ChangeRole(Guid id, ChangeMemberRoleCommand command, CancellationToken ct) =>
        Send(command with { MembershipId = id }, "Rol güncellendi.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("{id:guid}/remove")]
    public Task<IActionResult> Remove(Guid id, CancellationToken ct) =>
        Send(new RemoveMemberCommand(id), "Üye çıkarıldı.", ct);
}
