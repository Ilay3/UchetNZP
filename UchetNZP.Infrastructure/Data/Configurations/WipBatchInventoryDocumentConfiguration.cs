using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipBatchInventoryDocumentConfiguration : IEntityTypeConfiguration<WipBatchInventoryDocument>
{
    public void Configure(EntityTypeBuilder<WipBatchInventoryDocument> builder)
    {
        builder.ToTable("WipBatchInventoryDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InventoryNumber)
            .IsRequired();

        builder.Property(x => x.GeneratedAt)
            .IsRequired();

        builder.Property(x => x.ComposedAt)
            .IsRequired();

        builder.Property(x => x.PeriodFrom)
            .IsRequired();

        builder.Property(x => x.PeriodTo)
            .IsRequired();

        builder.Property(x => x.PartFilter)
            .HasMaxLength(256);

        builder.Property(x => x.SectionFilter)
            .HasMaxLength(256);

        builder.Property(x => x.OpNumberFilter)
            .HasMaxLength(16);

        builder.Property(x => x.RowCount)
            .IsRequired();

        builder.Property(x => x.TotalQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.FileName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired();

        builder.HasIndex(x => x.InventoryNumber)
            .IsUnique();

        builder.HasIndex(x => x.GeneratedAt);
    }
}
