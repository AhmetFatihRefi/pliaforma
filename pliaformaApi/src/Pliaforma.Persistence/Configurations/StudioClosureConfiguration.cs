using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudioClosureConfiguration : IEntityTypeConfiguration<StudioClosure>
{
    public void Configure(EntityTypeBuilder<StudioClosure> b)
    {
        b.ToTable("StudioClosures");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(150);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32);

        // Stüdyo-yerel duvar saati (seans saatleriyle aynı eksen).
        b.Property(x => x.StartAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.EndAt).HasColumnType("timestamp without time zone");

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.StartAt });
    }
}
