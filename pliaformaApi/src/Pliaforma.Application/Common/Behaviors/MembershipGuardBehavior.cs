using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Enums;
using Pliaforma.Domain.Exceptions;

namespace Pliaforma.Application.Common.Behaviors;

/// <summary>
/// Stüdyo-içi yazma (<see cref="IStudioWriteRequest"/>) ve hassas okuma (<see cref="IStudioReadRequest"/>)
/// isteklerinde, kullanıcının token'daki üyeliğinin DB'de HÂLÂ Active olduğunu VE token'daki rolünün
/// DB'deki güncel rolle uyuştuğunu doğrular. Token claim'ine kör güvenmez:
/// - Removed/Rejected → token süresi dolmadan engellenir (mevcut davranış).
/// - Rol düşürüldü (Sprint 48) → eski token'ın yüksek rolü token süresi dolmadan geçersizleşir (fail-closed).
///   Refresh token güncel rolle yeni access token ürettiğinden, yenilemeden sonra normal akış sürer.
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
        // Sprint 48: yazma + hassas PII/finans okuma isteklerinde üyelik/rol doğrulaması. Diğer istekler (auth,
        // platform, hassas-olmayan okumalar) dokunulmadan geçer.
        if (request is not (IStudioWriteRequest or IStudioReadRequest))
            return await next();

        var membershipId = _currentUser.MembershipId
            ?? throw new ForbiddenException("Aktif bir stüdyo üyeliği bağlamı yok.");

        var membership = await _db.StudioMemberships
            .Where(m => m.Id == membershipId)
            .Select(m => new { m.Status, m.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null || membership.Status != MembershipStatus.Active)
            throw new ForbiddenException("Stüdyo üyeliğiniz aktif değil; bu işlem yapılamaz.");

        // Sprint 48: token rolü ile DB rolü uyuşmuyorsa (ör. Owner rolü düşürdü/yükseltti) fail-closed.
        // Token claim'i null ise (beklenmez — stüdyo-bağlamlı token her zaman rol taşır) yalnız Status kontrolü geçerli.
        if (_currentUser.StudioRole is { } tokenRole && membership.Role != tokenRole)
            throw new ForbiddenException("Stüdyo yetki seviyeniz değişmiş; lütfen yeniden giriş yapın.");

        return await next();
    }
}
