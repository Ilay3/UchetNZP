using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalConsumptionNormConfiguration : IEntityTypeConfiguration<MetalConsumptionNorm>
{
    public void Configure(EntityTypeBuilder<MetalConsumptionNorm> builder)
    {
        builder.ToTable("MetalConsumptionNorms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SizeRaw)
            .HasMaxLength(128);

        builder.Property(x => x.ShapeType)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.DiameterMm)
            .HasPrecision(12, 3);

        builder.Property(x => x.ThicknessMm)
            .HasPrecision(12, 3);

        builder.Property(x => x.WidthMm)
            .HasPrecision(12, 3);

        builder.Property(x => x.LengthMm)
            .HasPrecision(12, 3);

        builder.Property(x => x.UnitNorm)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(x => x.ValueNorm)
            .HasPrecision(12, 6);

        builder.Property(x => x.ParseStatus)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.ParseError)
            .HasMaxLength(512);

        builder.Property(x => x.BaseConsumptionQty)
            .HasPrecision(12, 6)
            .IsRequired();

        builder.Property(x => x.ConsumptionUnit)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.SourceFile)
            .HasMaxLength(256);

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasOne(x => x.Part)
            .WithMany(x => x.MetalConsumptionNorms)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MetalMaterial)
            .WithMany(x => x.ConsumptionNorms)
            .HasForeignKey(x => x.MetalMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PartId, x.IsActive });
    }
}
