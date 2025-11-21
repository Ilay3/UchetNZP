using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class ReceiptAuditConfiguration : IEntityTypeConfiguration<ReceiptAudit>
{
    public void Configure(EntityTypeBuilder<ReceiptAudit> builder)
    {
        builder.ToTable("ReceiptAudits");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.PreviousQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.NewQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.ReceiptDate)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.Property(x => x.PreviousBalance)
            .HasPrecision(12, 3);

        builder.Property(x => x.NewBalance)
            .HasPrecision(12, 3);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
