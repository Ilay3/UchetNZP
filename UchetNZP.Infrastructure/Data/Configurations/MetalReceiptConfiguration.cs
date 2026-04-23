using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalReceiptConfiguration : IEntityTypeConfiguration<MetalReceipt>
{
    public void Configure(EntityTypeBuilder<MetalReceipt> builder)
    {
        builder.ToTable("MetalReceipts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.ReceiptDate)
            .IsRequired();

        builder.Property(x => x.SupplierOrSource)
            .HasMaxLength(256);

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.ReceiptNumber)
            .IsUnique();

        builder.HasIndex(x => x.ReceiptDate);
    }
}
