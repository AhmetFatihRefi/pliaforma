using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class AuthTokenConfiguration : IEntityTypeConfiguration<AuthToken>
{
    public void Configure(EntityTypeBuilder<AuthToken> b)
    {
        b.ToTable("AuthTokens");
        b.HasKey(x => x.Id);

        b.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.CreatedIp).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(400);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hash ile hızlı + benzersiz arama (silinmemiş kayıtlar).
        b.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.UserId, x.Purpose });
    }
}
