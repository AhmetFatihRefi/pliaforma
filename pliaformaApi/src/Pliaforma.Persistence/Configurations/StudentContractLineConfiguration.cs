using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

/// <summary>
/// Sprint 28A — StudentContractLine eşlemesi. StudentContractConfiguration deseni: yalnız Studio cascade; diğer
/// FK'ler Restrict (çoklu cascade yolundan kaçınma — Postgres). DueDate nullable. Status/Currency string.
/// </summary>
public sealed class StudentContractLineConfiguration : IEntityTypeConfiguration<StudentContractLine>
{
    public void Configure(EntityTypeBuilder<StudentContractLine> b)
    {
        b.ToTable("StudentContractLines");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Currency).IsRequired().HasMaxLength(8);
        b.Property(x => x.PricePerLesson).HasPrecision(12, 2);
        b.Property(x => x.TotalPrice).HasPrecision(12, 2);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Header sözleşmeye bağlı; çoklu cascade yolundan kaçınmak için Restrict (line'lar Studio cascade ile temizlenir).
        b.HasOne(x => x.StudentContract)
            .WithMany()
            .HasForeignKey(x => x.StudentContractId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ClassType)
            .WithMany()
            .HasForeignKey(x => x.ClassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.InstructorProfile)
            .WithMany()
            .HasForeignKey(x => x.InstructorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.StudioId, x.StudentContractId });
        b.HasIndex(x => new { x.StudioId, x.Status });
        b.HasIndex(x => new { x.StudioId, x.ClassTypeId, x.InstructorProfileId });
        b.HasIndex(x => new { x.StudioId, x.DueDate });
    }
}
