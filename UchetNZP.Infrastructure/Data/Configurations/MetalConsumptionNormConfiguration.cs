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
