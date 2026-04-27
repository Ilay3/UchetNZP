using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalReceiptItemConfiguration : IEntityTypeConfiguration<MetalReceiptItem>
{
    public void Configure(EntityTypeBuilder<MetalReceiptItem> builder)
    {
        builder.ToTable("MetalReceiptItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.TotalWeightKg)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.ItemIndex)
            .IsRequired();

        builder.Property(x => x.SizeValue)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.SizeUnitText)
            .IsRequired()
            .HasMaxLength(16);
        
        builder.Property(x => x.ActualBlankSizeText)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.PassportWeightKg)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.ActualWeightKg)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.CalculatedWeightKg)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.WeightDeviationKg)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.StockCategory)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.GeneratedCode)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.MetalReceipt)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.MetalReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MetalMaterial)
            .WithMany(x => x.ReceiptItems)
            .HasForeignKey(x => x.MetalMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MetalReceiptId, x.ItemIndex })
            .IsUnique();
    }
}
