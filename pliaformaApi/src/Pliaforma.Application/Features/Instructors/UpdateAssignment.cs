using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Instructors;

/// <summary>
/// Bir hoca↔grup atamasının ücret override'ını günceller. null verilirse override kaldırılır
/// (hoca profilindeki ana ücret geçerli olur). Negatif olamaz. Owner/StudioAdmin.
/// </summary>
public sealed record UpdateAssignmentCommand(Guid AssignmentId, decimal? RateOverride)
    : ICommand<InstructorAssignmentDto>, IStudioWriteRequest;

public sealed class UpdateAssignmentValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.RateOverride).GreaterThanOrEqualTo(0).When(x => x.RateOverride.HasValue);
    }
}

public sealed class UpdateAssignmentHandler : ICommandHandler<UpdateAssignmentCommand, InstructorAssignmentDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateAssignmentHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InstructorAssignmentDto> Handle(UpdateAssignmentCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.InstructorClassTypes
            .Include(x => x.ClassType)
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId && x.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Atama", request.AssignmentId);

        entity.RateOverride = request.RateOverride;
        await _db.SaveChangesAsync(cancellationToken);

        return new InstructorAssignmentDto(entity.Id, entity.ClassTypeId, entity.ClassType!.Name, entity.RateOverride);
    }
}
