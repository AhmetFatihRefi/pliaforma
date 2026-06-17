using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class StudioMakeupPolicyConfiguration : IEntityTypeConfiguration<StudioMakeupPolicy>
{
    public void Configure(EntityTypeBuilder<StudioMakeupPolicy> b)
    {
        b.ToTable("StudioMakeupPolicies");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Stüdyo başına tek politika kaydı.
        b.HasIndex(x => x.StudioId).IsUnique();
    }
}
