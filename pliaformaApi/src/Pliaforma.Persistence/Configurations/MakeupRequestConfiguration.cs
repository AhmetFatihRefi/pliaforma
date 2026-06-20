using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

/// <summary>
/// Sprint 27 — MakeupRequest eşlemesi. Yalnız Studio cascade FK; öğrenci/hoca/ders-tipi alanları PLAIN referans
/// (FK YOK → çoklu cascade yolundan kaçınma, mevcut ClassSession.ActualInstructorProfileId deseni gibi). StartAt/
/// EndAt duvar saati (timestamp without time zone, Unspecified); DecidedAt UTC instant (timestamp with time zone).
/// Index'ler: (StudioId,Status) owner listesi + conflict; (StudioId,RequestedInstructorProfileId,Status) hoca-çakışma
/// + "kendi talepleri"; (StudioId,StudentProfileId,Status) öğrenci-çakışma + öğrenci programı + rezerve sayımı.
/// </summary>
public sealed class MakeupRequestConfiguration : IEntityTypeConfiguration<MakeupRequest>
{
    public void Configure(EntityTypeBuilder<MakeupRequest> b)
    {
        b.ToTable("MakeupRequests");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.DecisionNote).HasMaxLength(1000);

        b.Property(x => x.StartAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.EndAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.DecidedAt).HasColumnType("timestamp with time zone");

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.Status });
        b.HasIndex(x => new { x.StudioId, x.RequestedInstructorProfileId, x.Status });
        b.HasIndex(x => new { x.StudioId, x.StudentProfileId, x.Status });
    }
}
