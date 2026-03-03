using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipLabelConfiguration : IEntityTypeConfiguration<WipLabel>
{
    public void Configure(EntityTypeBuilder<WipLabel> builder)
    {
        builder.ToTable("WipLabels");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LabelDate)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.RemainingQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.Number)
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.RootLabelId)
            .IsRequired();

        builder.Property(x => x.RootNumber)
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(x => x.Suffix)
            .IsRequired();

        builder.HasIndex(x => new { x.Status, x.CurrentSectionId, x.CurrentOpNumber });

        builder.HasIndex(x => x.RootLabelId);

        builder.HasIndex(x => x.Number)
            .IsUnique();

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WipLabels)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WipReceipt)
            .WithOne(x => x.WipLabel)
            .HasForeignKey<WipReceipt>(x => x.WipLabelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Transfers)
            .WithOne(x => x.WipLabel)
            .HasForeignKey(x => x.WipLabelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.WarehouseLabelItems)
            .WithOne(x => x.WipLabel)
            .HasForeignKey(x => x.WipLabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
