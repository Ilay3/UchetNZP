using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WarehouseItemConfiguration : IEntityTypeConfiguration<WarehouseItem>
{
    public void Configure(EntityTypeBuilder<WarehouseItem> builder)
    {
        builder.ToTable("WarehouseItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.AddedAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WarehouseItems)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Transfer)
            .WithOne(x => x.WarehouseItem)
            .HasForeignKey<WarehouseItem>(x => x.TransferId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.WarehouseLabelItems)
            .WithOne(x => x.WarehouseItem)
            .HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
