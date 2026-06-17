using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class ClassBookingConfiguration : IEntityTypeConfiguration<ClassBooking>
{
    public void Configure(EntityTypeBuilder<ClassBooking> b)
    {
        b.ToTable("ClassBookings");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.PackageEligibilityStatus).HasConversion<string>().HasMaxLength(24);

        // ExpectedStudentPackageId: plain referans (FK yok — paket soft-delete edilir, hard-delete olmaz).
        // Yoklama tüketiminde kullanılabilir hak hesabı için indekslenir.
        b.HasIndex(x => new { x.StudioId, x.ExpectedStudentPackageId });

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ClassSession)
            .WithMany()
            .HasForeignKey(x => x.ClassSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.ClassSessionId });
        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });

        // Çift-rezervasyon koruması (race-safe): aynı seans+öğrenci için yalnızca bir AKTİF (Booked,
        // silinmemiş) kayıt. PostgreSQL filtered (partial) unique index.
        b.HasIndex(x => new { x.ClassSessionId, x.StudentProfileId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Booked' AND \"IsDeleted\" = false");
    }
}
