using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Common.Behaviors;

/// <summary>
/// Stüdyo-içi yazma isteklerinde, kullanıcının token'daki üyeliğinin DB'de HÂLÂ Active
/// olduğunu doğrular. Token claim'ine kör güvenmez: kullanıcı stüdyodan çıkarıldıysa
/// (Removed/Rejected) token süresi dolmadan da işlemi engeller.
/// </summary>
public sealed class MembershipGuardBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MembershipGuardBehavior(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IStudioWriteRequest)
            return await next();

        var membershipId = _currentUser.MembershipId
            ?? throw new ForbiddenException("Aktif bir stüdyo üyeliği bağlamı yok.");

        var status = await _db.StudioMemberships
            .Where(m => m.Id == membershipId)
            .Select(m => (MembershipStatus?)m.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status != MembershipStatus.Active)
            throw new ForbiddenException("Stüdyo üyeliğiniz aktif değil; bu işlem yapılamaz.");

        return await next();
    }
}
