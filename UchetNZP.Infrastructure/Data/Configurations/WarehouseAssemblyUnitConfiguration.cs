using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WarehouseAssemblyUnitConfiguration : IEntityTypeConfiguration<WarehouseAssemblyUnit>
{
    public void Configure(EntityTypeBuilder<WarehouseAssemblyUnit> builder)
    {
        builder.ToTable("WarehouseAssemblyUnits");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.NormalizedName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.NormalizedName)
            .IsUnique();
    }
}
