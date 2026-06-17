using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("SubscriptionPlans");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Tier).HasMaxLength(50);
        b.Property(x => x.MonthlyPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.IntroMonthlyPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        b.Property(x => x.BillingPeriod).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.FeaturesJson).HasMaxLength(4000);

        // Aynı anda yalnız BİR aktif default plan olur. PostgreSQL filtered (partial) unique index.
        b.HasIndex(x => x.IsDefault)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"IsActive\" = true AND \"IsDeleted\" = false");
    }
}
