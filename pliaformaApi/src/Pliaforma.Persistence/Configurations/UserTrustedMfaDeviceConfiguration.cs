using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class UserTrustedMfaDeviceConfiguration : IEntityTypeConfiguration<UserTrustedMfaDevice>
{
    public void Configure(EntityTypeBuilder<UserTrustedMfaDevice> b)
    {
        b.ToTable("UserTrustedMfaDevices");
        b.HasKey(x => x.Id);

        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);   // RevokeAll + kullanıcı bazlı sorgu
    }
}
