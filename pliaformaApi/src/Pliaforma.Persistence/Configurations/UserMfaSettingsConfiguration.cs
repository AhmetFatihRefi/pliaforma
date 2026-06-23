using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class UserMfaSettingsConfiguration : IEntityTypeConfiguration<UserMfaSettings>
{
    public void Configure(EntityTypeBuilder<UserMfaSettings> b)
    {
        b.ToTable("UserMfaSettings");
        b.HasKey(x => x.Id);

        b.Property(x => x.SecretEncrypted).HasMaxLength(512);
        b.Property(x => x.PendingSecretEncrypted).HasMaxLength(512);

        b.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsDeleted\" = false");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
