using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Common.Behaviors;

/// <summary>
/// Stüdyo-içi yazma isteklerinde (IStudioWriteRequest) stüdyonun aktif olduğunu doğrular.
/// Yalnızca Status == Active iken yazma yapılabilir; Suspended/Cancelled stüdyolar yazamaz
/// (platform admin askıya almıştır). Okuma etkilenmez.
/// </summary>
public sealed class StudioStatusBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StudioStatusBehavior(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IStudioWriteRequest)
            return await next();

        var studioId = _currentUser.RequireStudioId();
        var status = await _db.Studios
            .Where(s => s.Id == studioId)
            .Select(s => (StudioStatus?)s.Status)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Stüdyo", studioId);

        if (status != StudioStatus.Active)
            throw new ForbiddenException("Stüdyonuz askıya alındığı için bu işlem yapılamaz.");

        return await next();
    }
}
