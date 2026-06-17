using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Features.ClassTypes;

/// <summary>Bir pilates grubunu siler (soft-delete). Yalnızca aktif stüdyonun grubu.</summary>
public sealed record DeleteClassTypeCommand(Guid Id) : ICommand<Unit>, IStudioWriteRequest;

public sealed class DeleteClassTypeHandler : ICommandHandler<DeleteClassTypeCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteClassTypeHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteClassTypeCommand request, CancellationToken cancellationToken)
    {
        var studioId = _currentUser.RequireStudioId();

        var entity = await _db.ClassTypes
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.StudioId == studioId, cancellationToken)
            ?? throw new NotFoundException("Ders tipi", request.Id);

        _db.ClassTypes.Remove(entity); // AuditInterceptor soft-delete'e çevirir
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
