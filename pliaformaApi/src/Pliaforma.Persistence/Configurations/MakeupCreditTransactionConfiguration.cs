using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class MakeupCreditTransactionConfiguration : IEntityTypeConfiguration<MakeupCreditTransaction>
{
    public void Configure(EntityTypeBuilder<MakeupCreditTransaction> b)
    {
        b.ToTable("MakeupCreditTransactions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reason).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Notes).HasMaxLength(500);

        // ExpiresAt UTC (timestamptz). Audit alanları zaten UTC.
        b.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentProfile)
            .WithMany()
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sprint 28A: kredinin doğduğu sözleşme SATIRI (line). Line silinse de ledger izi kalır (SetNull).
        b.HasOne(x => x.StudentContractLine)
            .WithMany()
            .HasForeignKey(x => x.StudentContractLineId)
            .OnDelete(DeleteBehavior.SetNull);

        // Bakiye hesabı ve geçmiş listesi için.
        b.HasIndex(x => new { x.StudioId, x.StudentProfileId });
        // Sprint 28A: line-bazlı bakiye (28C) için.
        b.HasIndex(x => new { x.StudioId, x.StudentContractLineId });

        // Bir kaynak seans iptali için aynı öğrenciye yalnızca BİR otomatik Granted hareket
        // (StudioCancelledClass/HolidayClosure). Filtered (partial) unique index.
        b.HasIndex(x => new { x.StudentProfileId, x.SourceClassSessionId })
            .IsUnique()
            .HasFilter("\"Type\" = 'Granted' AND \"SourceClassSessionId\" IS NOT NULL AND \"IsDeleted\" = false");

        // Bir telafi rezervasyonu için yalnızca BİR Used hareket (aynı krediyi iki kez tüketmeyi engeller).
        b.HasIndex(x => x.UsedForBookingId)
            .IsUnique()
            .HasFilter("\"UsedForBookingId\" IS NOT NULL AND \"IsDeleted\" = false");
    }
}
