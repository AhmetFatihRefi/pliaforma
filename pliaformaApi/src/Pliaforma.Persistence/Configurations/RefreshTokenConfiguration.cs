using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;
using Pliaforma.Domain.Enums;

namespace Pliaforma.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);

        b.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);

        // Sprint 53: kanal bağlama — string saklanır (yeni AuthClient değeri migration gerektirmez),
        // mevcut satırlar DEFAULT 'Web' alır (hepsi cookie ile verilmişti; doğru sınıflama).
        b.Property(x => x.Client).HasConversion<string>().HasMaxLength(16).HasDefaultValue(AuthClient.Web);

        b.HasIndex(x => x.TokenHash).IsUnique();

        b.Ignore(x => x.IsActive);
    }
}
