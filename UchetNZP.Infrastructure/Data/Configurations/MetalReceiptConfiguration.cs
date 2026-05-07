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
            .HasMaxLength(128);

        builder.Property(x => x.ReceiptDate)
            .IsRequired();

        builder.Property(x => x.SupplierOrSource)
            .HasMaxLength(256);

        builder.Property(x => x.SupplierIdentifierSnapshot)
            .HasMaxLength(64);

        builder.Property(x => x.SupplierNameSnapshot)
            .HasMaxLength(256);

        builder.Property(x => x.SupplierInnSnapshot)
            .HasMaxLength(12);

        builder.Property(x => x.SupplierDocumentNumber)
            .HasMaxLength(128);
        builder.Property(x => x.InvoiceOrUpiNumber)
            .HasMaxLength(128);
        builder.Property(x => x.AccountingAccount)
            .IsRequired()
            .HasMaxLength(16);
        builder.Property(x => x.VatAccount)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.BatchNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.Property(x => x.PricePerKg)
            .HasPrecision(14, 4)
            .IsRequired();

        builder.Property(x => x.AmountWithoutVat)
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(x => x.VatRatePercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.VatAmount)
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(x => x.TotalAmountWithVat)
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(x => x.OriginalDocumentFileName)
            .HasMaxLength(260);

        builder.Property(x => x.OriginalDocumentContentType)
            .HasMaxLength(128);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.ReceiptNumber)
            .IsUnique();

        builder.HasIndex(x => x.ReceiptDate);

        builder.HasIndex(x => x.MetalSupplierId);

        builder.HasOne(x => x.MetalSupplier)
            .WithMany(x => x.Receipts)
            .HasForeignKey(x => x.MetalSupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
