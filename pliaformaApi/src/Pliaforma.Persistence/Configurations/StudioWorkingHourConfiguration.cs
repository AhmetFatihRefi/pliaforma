using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

/// <summary>
/// Sprint 37 — StudioWorkingHour eşlemesi. Studio cascade; DayOfWeek string saklanır; OpensAt/ClosesAt TimeOnly?
/// (time without time zone). Tekillik (StudioId, DayOfWeek) DB unique index ile DEĞİL uygulama katmanında garanti
/// edilir (full-replace soft-delete'li slotlarla çakışmasın diye — InstructorAvailability/PreferredSlot deseni).
/// </summary>
public sealed class StudioWorkingHourConfiguration : IEntityTypeConfiguration<StudioWorkingHour>
{
    public void Configure(EntityTypeBuilder<StudioWorkingHour> b)
    {
        b.ToTable("StudioWorkingHours");
        b.HasKey(x => x.Id);

        b.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(12);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.DayOfWeek });
    }
}
