using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class PackagePlanPriceVersionConfiguration : IEntityTypeConfiguration<PackagePlanPriceVersion>
{
    public void Configure(EntityTypeBuilder<PackagePlanPriceVersion> b)
    {
        b.ToTable("PackagePlanPriceVersions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Price).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PackagePlan)
            .WithMany(p => p.PriceVersions)
            .HasForeignKey(x => x.PackagePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.PackagePlanId, x.EffectiveFrom });
    }
}
