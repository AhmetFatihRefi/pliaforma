using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Body).IsRequired().HasMaxLength(1000);
        b.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(200);
        b.Property(x => x.FailureReason).HasMaxLength(500);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Recipient)
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kullanıcının bildirim listesi + okunmamış sayımı için.
        b.HasIndex(x => new { x.StudioId, x.RecipientUserId });

        // Aynı olay için ikinci bildirim engeli (idempotency): stüdyo içinde dedup anahtarı benzersiz.
        b.HasIndex(x => new { x.StudioId, x.DeduplicationKey })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
