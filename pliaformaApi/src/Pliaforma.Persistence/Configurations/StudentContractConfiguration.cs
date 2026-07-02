using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudentContractConfiguration : IEntityTypeConfiguration<StudentContract>
{
    public void Configure(EntityTypeBuilder<StudentContract> b)
    {
        b.ToTable("StudentContracts");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Currency).IsRequired().HasMaxLength(8);
        b.Property(x => x.Price).HasPrecision(12, 2);
        b.Property(x => x.Notes).HasMaxLength(2000);
        // Sprint 46: pilot işareti. Default false → mevcut satırlar migration'da false alır.
        b.Property(x => x.IsPilot).HasDefaultValue(false);

        // Studio cascade; diğer FK'ler Restrict (tek cascade yolu — Postgres çoklu cascade'i sevmez).
        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ClassType)
            .WithMany()
            .HasForeignKey(x => x.ClassTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.InstructorProfile)
            .WithMany()
            .HasForeignKey(x => x.InstructorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });
        b.HasIndex(x => new { x.StudioId, x.Status });
    }
}
