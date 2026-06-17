using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class PlatformSubscriptionPaymentConfiguration : IEntityTypeConfiguration<PlatformSubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<PlatformSubscriptionPayment> b)
    {
        b.ToTable("PlatformSubscriptionPayments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Amount).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        b.Property(x => x.BillingPeriod).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ProviderRef).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PlatformSubscription)
            .WithMany()
            .HasForeignKey(x => x.PlatformSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.PaidAt });
        b.HasIndex(x => x.PlatformSubscriptionId);
        // Platform geneli "bu ay manuel tahsilat" özeti için.
        b.HasIndex(x => new { x.Source, x.PaidAt });
    }
}
