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

        builder.Property(x => x.PartId)
            .IsRequired(false);

        builder.Property(x => x.AssemblyUnitId)
            .IsRequired(false);

        builder.Property(x => x.MovementType)
            .HasMaxLength(32)
            .HasDefaultValue(WarehouseMovementKind.Receipt)
            .IsRequired();

        builder.Property(x => x.SourceType)
            .HasMaxLength(32)
            .HasDefaultValue(WarehouseMovementKind.AutomaticTransfer)
            .IsRequired();

        builder.Property(x => x.DocumentNumber)
            .HasMaxLength(64);

        builder.Property(x => x.ControlCardNumber)
            .HasMaxLength(64);

        builder.Property(x => x.ControllerName)
            .HasMaxLength(128);

        builder.Property(x => x.MasterName)
            .HasMaxLength(128);

        builder.Property(x => x.AcceptedByName)
            .HasMaxLength(128);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.AddedAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WarehouseItems)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AssemblyUnit)
            .WithMany(x => x.WarehouseItems)
            .HasForeignKey(x => x.AssemblyUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MovementType);

        builder.HasIndex(x => x.SourceType);

        builder.HasIndex(x => x.AssemblyUnitId);

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
