using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pliaforma.Domain.Entities;

namespace Pliaforma.Persistence.Configurations;

/// <summary>
/// Sprint 38 — ContractLinePaymentInstallment eşlemesi. Studio + line cascade (line gidince taksitler de gider).
/// Amount numeric(12,2). Tekillik DB unique index ile DEĞİL uygulama katmanında (full-replace soft-delete deseni —
/// StudentContractLinePreferredSlot precedent). Status/Currency saklanmaz (hesaplanan / line'dan).
/// </summary>
public sealed class ContractLinePaymentInstallmentConfiguration : IEntityTypeConfiguration<ContractLinePaymentInstallment>
{
    public void Configure(EntityTypeBuilder<ContractLinePaymentInstallment> b)
    {
        b.ToTable("ContractLinePaymentInstallments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Studio)
            .WithMany()
            .HasForeignKey(x => x.StudioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.StudentContractLine)
            .WithMany()
            .HasForeignKey(x => x.StudentContractLineId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.StudioId, x.StudentContractLineId });
    }
}
