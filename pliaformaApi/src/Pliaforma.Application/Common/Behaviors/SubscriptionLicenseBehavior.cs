using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Application.Features.Platform;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Common.Behaviors;

/// <summary>
/// Stüdyo-içi YAZMA isteklerinde (IStudioWriteRequest) aboneliğin LİSANSLI olduğunu doğrular. Lisanssız
/// (süresi dolmuş, grace bitmiş) stüdyo yazamaz → 403. Okuma + login etkilenmez. **Admin-suspend kapısından
/// (StudioStatusBehavior) BAĞIMSIZ ikinci kapıdır.** **Fail-open:** aboneliği OLMAYAN stüdyo lisanslı sayılır
/// (yanlışlıkla kilitlememek için; platform health'te missingSubscriptionCount ile görünür). Platform uçları
/// IStudioWriteRequest taşımadığından bu kapıya hiç takılmaz.
/// </summary>
public sealed class SubscriptionLicenseBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public SubscriptionLicenseBehavior(IAppDbContext db, ICurrentUserService currentUser, IDateTime clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IStudioWriteRequest)
            return await next();

        var studioId = _currentUser.RequireStudioId();
        var sub = await _db.PlatformSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudioId == studioId, cancellationToken);

        // sub null → fail-open (lisanslı). Aksi halde dinamik lisans.
        var lic = SubscriptionRules.Evaluate(sub, DateOnly.FromDateTime(_clock.UtcNow));
        if (!lic.IsLicensed)
            throw new ForbiddenException(lic.LicenseMessage);

        return await next();
    }
}
