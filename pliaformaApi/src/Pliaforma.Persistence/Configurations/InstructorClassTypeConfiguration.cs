using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class InstructorClassTypeConfiguration : IEntityTypeConfiguration<InstructorClassType>
{
    public void Configure(EntityTypeBuilder<InstructorClassType> b)
    {
        b.ToTable("InstructorClassTypes");
        b.HasKey(x => x.Id);

        b.Property(x => x.RateOverride).HasPrecision(18, 2);

        b.HasOne(x => x.InstructorProfile)
            .WithMany(p => p.ClassTypes)
            .HasForeignKey(x => x.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ClassType)
            .WithMany(c => c.Instructors)
            .HasForeignKey(x => x.ClassTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Aynı hoca-grup ikilisi tek kez.
        b.HasIndex(x => new { x.InstructorProfileId, x.ClassTypeId }).IsUnique();
    }
}
