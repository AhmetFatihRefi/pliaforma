using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class PackagePlanClassTypeConfiguration : IEntityTypeConfiguration<PackagePlanClassType>
{
    public void Configure(EntityTypeBuilder<PackagePlanClassType> b)
    {
        b.ToTable("PackagePlanClassTypes");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PackagePlan)
            .WithMany(p => p.ClassTypes)
            .HasForeignKey(x => x.PackagePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ClassType)
            .WithMany()
            .HasForeignKey(x => x.ClassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Aynı plan+class type için yalnız bir aktif kayıt.
        b.HasIndex(x => new { x.PackagePlanId, x.ClassTypeId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
