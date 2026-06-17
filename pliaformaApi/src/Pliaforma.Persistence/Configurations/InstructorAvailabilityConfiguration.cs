using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class InstructorAvailabilityConfiguration : IEntityTypeConfiguration<InstructorAvailability>
{
    public void Configure(EntityTypeBuilder<InstructorAvailability> b)
    {
        b.ToTable("InstructorAvailabilities");
        b.HasKey(x => x.Id);

        b.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(12);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.InstructorProfile)
            .WithMany()
            .HasForeignKey(x => x.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.InstructorProfileId, x.DayOfWeek });
    }
}
