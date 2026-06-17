using Microsoft.EntityFrameworkCore;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Interfaces;

namespace Pliaforma.Application.Features.Platform;

/// <summary>
/// Platform geneli son aktivite (PlatformAdmin). Cross-tenant; birkaç kaynaktan az kayıt çekilip
/// zamana göre birleştirilir (ağır sorgu yok). READ-ONLY.
/// </summary>
public sealed record GetPlatformRecentActivityQuery : IQuery<IReadOnlyList<PlatformActivityItemDto>>;

public sealed class GetPlatformRecentActivityHandler : IQueryHandler<GetPlatformRecentActivityQuery, IReadOnlyList<PlatformActivityItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPlatformRecentActivityHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PlatformActivityItemDto>> Handle(GetPlatformRecentActivityQuery request, CancellationToken ct)
    {
        PlatformRules.EnsurePlatformAdmin(_currentUser);

        var items = new List<PlatformActivityItemDto>();

        var newStudios = await _db.Studios.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).Take(10)
            .Select(s => new { s.Name, s.CreatedAt }).ToListAsync(ct);
        items.AddRange(newStudios.Select(s => new PlatformActivityItemDto("StudioCreated", $"Yeni stüdyo: {s.Name}", s.CreatedAt)));

        var newPayments = await _db.Payments.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt).Take(10)
            .Select(p => new { p.Amount, p.Currency, p.CreatedAt }).ToListAsync(ct);
        items.AddRange(newPayments.Select(p => new PlatformActivityItemDto("Payment", $"Ödeme {p.Amount:0.##} {p.Currency}", p.CreatedAt)));

        var newNotifications = await _db.Notifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt).Take(10)
            .Select(n => new { n.Title, n.Status, n.CreatedAt }).ToListAsync(ct);
        items.AddRange(newNotifications.Select(n => new PlatformActivityItemDto("Notification", $"{n.Title} ({n.Status})", n.CreatedAt)));

        return items.OrderByDescending(i => i.AtUtc).Take(20).ToList();
    }
}
