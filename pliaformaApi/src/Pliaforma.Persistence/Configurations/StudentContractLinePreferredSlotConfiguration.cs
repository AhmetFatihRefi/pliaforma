using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

/// <summary>
/// Sprint 29A — StudentContractLinePreferredSlot eşlemesi. Yalnız Studio cascade; line FK cascade (line gidince
/// slotlar da gider). DayOfWeek string saklanır. Tekillik kontrolü uygulama katmanında (NULL saat karşılaştırması
/// filtered unique index ile garanti edilemez; handler/validator kontrol eder).
/// </summary>
public sealed class StudentContractLinePreferredSlotConfiguration : IEntityTypeConfiguration<StudentContractLinePreferredSlot>
{
    public void Configure(EntityTypeBuilder<StudentContractLinePreferredSlot> b)
    {
        b.ToTable("StudentContractLinePreferredSlots");
        b.HasKey(x => x.Id);

        b.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentContractLine)
            .WithMany()
            .HasForeignKey(x => x.StudentContractLineId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.StudentContractLineId });
        b.HasIndex(x => new { x.StudioId, x.DayOfWeek });
    }
}
