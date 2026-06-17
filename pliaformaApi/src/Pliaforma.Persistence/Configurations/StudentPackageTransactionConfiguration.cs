using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudentPackageTransactionConfiguration : IEntityTypeConfiguration<StudentPackageTransaction>
{
    public void Configure(EntityTypeBuilder<StudentPackageTransaction> b)
    {
        b.ToTable("StudentPackageTransactions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reason).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentPackage)
            .WithMany()
            .HasForeignKey(x => x.StudentPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.StudentPackageId });
        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });

        // Bir yoklamadan en fazla BİR kullanım (Used) hareketi → çift düşümü DB seviyesinde engeller.
        b.HasIndex(x => x.SourceAttendanceId)
            .IsUnique()
            .HasFilter("\"Type\" = 'Used' AND \"SourceAttendanceId\" IS NOT NULL AND \"IsDeleted\" = false");
    }
}
