using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalStockMovementConfiguration : IEntityTypeConfiguration<MetalStockMovement>
{
    public void Configure(EntityTypeBuilder<MetalStockMovement> builder)
    {
        builder.ToTable("MetalStockMovements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementDate)
            .IsRequired();

        builder.Property(x => x.MovementType)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.SourceDocumentType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.QtyBefore)
            .HasPrecision(12, 3);

        builder.Property(x => x.QtyChange)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.QtyAfter)
            .HasPrecision(12, 3);

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasOne(x => x.MetalMaterial)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.MetalMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MetalReceiptItem)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.MetalReceiptItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MovementDate);
        builder.HasIndex(x => x.MetalReceiptItemId);
        builder.HasIndex(x => new { x.SourceDocumentType, x.SourceDocumentId });
    }
}
