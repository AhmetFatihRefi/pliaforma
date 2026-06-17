using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Features.Instructors;

namespace Pliaforma.Api.Controllers;

[Authorize]
public sealed class InstructorsController : ApiControllerBase
{
    public InstructorsController(IDispatcher dispatcher) : base(dispatcher) { }

    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken ct) =>
        Send(new GetMyInstructorProfileQuery(), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPut("{profileId:guid}/rate")]
    public Task<IActionResult> UpdateRate(Guid profileId, UpdateInstructorRateCommand command, CancellationToken ct) =>
        Send(command with { InstructorProfileId = profileId }, "Ücret güncellendi.", ct);

    [HttpGet("{profileId:guid}/class-types")]
    public Task<IActionResult> GetClassTypes(Guid profileId, CancellationToken ct) =>
        Send(new GetInstructorClassTypesQuery(profileId), ct: ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPost("{profileId:guid}/class-types")]
    public Task<IActionResult> Assign(Guid profileId, AssignClassTypeCommand command, CancellationToken ct) =>
        Send(command with { InstructorProfileId = profileId }, "Hoca gruba atandı.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpPut("class-types/{assignmentId:guid}")]
    public Task<IActionResult> UpdateAssignment(Guid assignmentId, UpdateAssignmentCommand command, CancellationToken ct) =>
        Send(command with { AssignmentId = assignmentId }, "Atama güncellendi.", ct);

    [Authorize(Roles = "Owner,StudioAdmin")]
    [HttpDelete("class-types/{assignmentId:guid}")]
    public Task<IActionResult> Unassign(Guid assignmentId, CancellationToken ct) =>
        Send(new UnassignClassTypeCommand(assignmentId), "Atama kaldırıldı.", ct);
}
