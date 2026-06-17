using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudentPackageAllowedClassTypeConfiguration : IEntityTypeConfiguration<StudentPackageAllowedClassType>
{
    public void Configure(EntityTypeBuilder<StudentPackageAllowedClassType> b)
    {
        b.ToTable("StudentPackageAllowedClassTypes");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentPackage)
            .WithMany(p => p.AllowedClassTypes)
            .HasForeignKey(x => x.StudentPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ClassType)
            .WithMany()
            .HasForeignKey(x => x.ClassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.StudentPackageId, x.ClassTypeId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
