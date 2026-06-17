using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudioMembershipConfiguration : IEntityTypeConfiguration<StudioMembership>
{
    public void Configure(EntityTypeBuilder<StudioMembership> b)
    {
        b.ToTable("StudioMemberships");
        b.HasKey(x => x.Id);

        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(32);

        b.HasOne(x => x.Studio)
            .WithMany(s => s.Memberships)
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir kullanıcı aynı stüdyoda en fazla bir üyeliğe sahip olur.
        b.HasIndex(x => new { x.StudioId, x.UserId }).IsUnique();
    }
}
