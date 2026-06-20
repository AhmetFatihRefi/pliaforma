using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Amount).HasColumnType("numeric(12,2)");
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentPackage)
            .WithMany()
            .HasForeignKey(x => x.StudentPackageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sprint 23: ödeme → sözleşme bağı (opsiyonel). Sözleşme silinirse ödeme etiketsiz kalır (SetNull).
        b.HasOne(x => x.StudentContract)
            .WithMany()
            .HasForeignKey(x => x.StudentContractId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sprint 28A: ödeme → sözleşme SATIRI bağı (line). Line silinirse etiketsiz kalır (SetNull).
        b.HasOne(x => x.StudentContractLine)
            .WithMany()
            .HasForeignKey(x => x.StudentContractLineId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });
        b.HasIndex(x => new { x.StudioId, x.Status });
        b.HasIndex(x => new { x.StudioId, x.StudentContractId });
        b.HasIndex(x => new { x.StudioId, x.StudentContractLineId });
        // Vade bazlı taramalar (hatırlatma: due-soon/overdue; dashboard/rapor DueDate aralığı) için.
        b.HasIndex(x => new { x.StudioId, x.DueDate });
    }
}
