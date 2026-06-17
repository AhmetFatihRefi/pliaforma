using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class PlatformSubscriptionConfiguration : IEntityTypeConfiguration<PlatformSubscription>
{
    public void Configure(EntityTypeBuilder<PlatformSubscription> b)
    {
        b.ToTable("PlatformSubscriptions");
        b.HasKey(x => x.Id);

        b.Property(x => x.PlanName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Tier).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.BillingPeriod).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.MonthlyPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir stüdyonun yalnızca bir (silinmemiş) aboneliği olur. PostgreSQL filtered (partial) unique index.
        b.HasIndex(x => x.StudioId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
