using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalMaterialConfiguration : IEntityTypeConfiguration<MetalMaterial>
{
    public void Configure(EntityTypeBuilder<MetalMaterial> builder)
    {
        builder.ToTable("MetalMaterials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(64);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.WeightPerUnitKg)
            .HasPrecision(12, 6);

        builder.Property(x => x.Coefficient)
            .HasPrecision(12, 6)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(256);

        builder.Property(x => x.UnitKind)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();
    }
}
