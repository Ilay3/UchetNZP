using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WarehouseLabelItemConfiguration : IEntityTypeConfiguration<WarehouseLabelItem>
{
    public void Configure(EntityTypeBuilder<WarehouseLabelItem> builder)
    {
        builder.ToTable("WarehouseLabelItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.AddedAt)
            .IsRequired();

        builder.HasOne(x => x.WarehouseItem)
            .WithMany(x => x.WarehouseLabelItems)
            .HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WipLabel)
            .WithMany(x => x.WarehouseLabelItems)
            .HasForeignKey(x => x.WipLabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
