using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

/// <summary>
/// SecurityAuditLog yapılandırması. BaseEntity olmadığından soft-delete filtresine ve AuditInterceptor'a
/// TAKILMAZ (AppDbContext.OnModelCreating yalnız BaseEntity'lere uygular). Append-only; index'ler sorgu içindir.
/// </summary>
public sealed class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> b)
    {
        b.ToTable("SecurityAuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();   // istemci tarafı GUID

        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();

        b.Property(x => x.ActorEmailMasked).HasMaxLength(256);
        b.Property(x => x.TargetType).HasMaxLength(64);
        b.Property(x => x.Ip).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.Property(x => x.Detail).HasMaxLength(512);

        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        b.HasIndex(x => new { x.StudioId, x.OccurredAt });
        b.HasIndex(x => x.EventType);
    }
}
