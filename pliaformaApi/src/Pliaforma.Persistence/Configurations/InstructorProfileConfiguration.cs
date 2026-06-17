using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class InstructorProfileConfiguration : IEntityTypeConfiguration<InstructorProfile>
{
    public void Configure(EntityTypeBuilder<InstructorProfile> b)
    {
        b.ToTable("InstructorProfiles");
        b.HasKey(x => x.Id);

        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Bio).HasMaxLength(1000);
        b.Property(x => x.Rate).HasPrecision(18, 2);
        b.Property(x => x.RateType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Color).HasMaxLength(16);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir kullanıcı aynı stüdyoda tek hoca profiline sahip olur.
        b.HasIndex(x => new { x.StudioId, x.UserId }).IsUnique();
    }
}
