using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class ClassTypeConfiguration : IEntityTypeConfiguration<ClassType>
{
    public void Configure(EntityTypeBuilder<ClassType> b)
    {
        b.ToTable("ClassTypes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Equipment).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CapacityType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.Name });
    }
}
