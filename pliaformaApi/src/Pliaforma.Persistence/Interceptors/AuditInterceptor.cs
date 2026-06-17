using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pliaforma.Application.Common.Interfaces;
using Pliaforma.Domain.Common;

namespace Pliaforma.Persistence.Interceptors;

/// <summary>
/// SaveChanges sırasında audit alanlarını doldurur ve hard-delete yerine soft-delete uygular:
/// Added → CreatedAt; Modified → UpdatedAt; Deleted → IsDeleted=true + UpdatedAt.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IDateTime _clock;

    public AuditInterceptor(IDateTime clock) => _clock = clock;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
            return;

        var now = _clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
