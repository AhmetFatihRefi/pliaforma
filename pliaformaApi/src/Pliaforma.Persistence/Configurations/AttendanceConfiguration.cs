using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> b)
    {
        b.ToTable("Attendances");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ClassBooking)
            .WithMany()
            .HasForeignKey(x => x.ClassBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seans bazlı yoklama listesi + öğrenci geçmişi için indeksler.
        b.HasIndex(x => new { x.StudioId, x.ClassSessionId });
        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });

        // Her booking için en fazla bir aktif yoklama kaydı. PostgreSQL filtered (partial) unique index.
        b.HasIndex(x => x.ClassBookingId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
