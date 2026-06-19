using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

public sealed class ContractAmendmentConfiguration : IEntityTypeConfiguration<ContractAmendment>
{
    public void Configure(EntityTypeBuilder<ContractAmendment> b)
    {
        b.ToTable("ContractAmendments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Currency).IsRequired().HasMaxLength(8);
        b.Property(x => x.AdditionalPrice).HasPrecision(12, 2);
        b.Property(x => x.Description).HasMaxLength(1000);

        // Studio cascade; sözleşme FK Restrict (tek cascade yolu — Postgres çoklu cascade'i sevmez).
        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentContract)
            .WithMany()
            .HasForeignKey(x => x.StudentContractId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.StudioId, x.StudentContractId });
    }
}
