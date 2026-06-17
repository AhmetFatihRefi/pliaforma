using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> b)
    {
        b.ToTable("ClassSessions");
        b.HasKey(x => x.Id);

        // Stüdyo-yerel duvar saati: TZ dönüşümü yok → "timestamp without time zone".
        b.Property(x => x.StartAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.EndAt).HasColumnType("timestamp without time zone");

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SessionKind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.RescheduleReason).HasMaxLength(500);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.InstructorProfile)
            .WithMany()
            .HasForeignKey(x => x.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ClassType)
            .WithMany()
            .HasForeignKey(x => x.ClassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Çakışma sorgusu + takvim listesi için indeksler.
        b.HasIndex(x => new { x.StudioId, x.InstructorProfileId, x.StartAt });
        b.HasIndex(x => new { x.StudioId, x.StartAt });
    }
}
