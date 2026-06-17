using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.Instructors;

/// <summary>Bir hoca↔grup atamasını kaldırır (aktif stüdyo kapsamında). Owner/StudioAdmin.</summary>
public sealed record UnassignClassTypeCommand(Guid AssignmentId) : ICommand<Unit>, IStudioWriteRequest;

public sealed class UnassignClassTypeHandler : ICommandHandler<UnassignClassTypeCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnassignClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UnassignClassTypeCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.InstructorClassTypes
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId && x.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Atama", request.AssignmentId);

        _db.InstructorClassTypes.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
