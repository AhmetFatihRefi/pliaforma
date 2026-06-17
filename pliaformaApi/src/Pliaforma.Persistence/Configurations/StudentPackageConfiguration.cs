using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudentPackageConfiguration : IEntityTypeConfiguration<StudentPackage>
{
    public void Configure(EntityTypeBuilder<StudentPackage> b)
    {
        b.ToTable("StudentPackages");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Price).HasColumnType("numeric(12,2)");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PackagePlan)
            .WithMany()
            .HasForeignKey(x => x.PackagePlanId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });
    }
}
