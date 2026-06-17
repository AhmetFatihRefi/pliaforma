using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class PackagePlanConfiguration : IEntityTypeConfiguration<PackagePlan>
{
    public void Configure(EntityTypeBuilder<PackagePlan> b)
    {
        b.ToTable("PackagePlans");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Price).HasColumnType("numeric(12,2)");

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Aynı stüdyoda paket adı benzersiz (aktif kayıtlar arasında).
        b.HasIndex(x => new { x.StudioId, x.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
