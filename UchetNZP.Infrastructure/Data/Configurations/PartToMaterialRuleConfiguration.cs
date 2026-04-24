using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class PartToMaterialRuleConfiguration : IEntityTypeConfiguration<PartToMaterialRule>
{
    public void Configure(EntityTypeBuilder<PartToMaterialRule> builder)
    {
        builder.ToTable("part_to_material_rule");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartNamePattern)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.GeometryType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.RolledType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.SizeFromMm)
            .HasPrecision(12, 3);

        builder.Property(x => x.SizeToMm)
            .HasPrecision(12, 3);

        builder.Property(x => x.MaterialGradePattern)
            .HasMaxLength(128);

        builder.Property(x => x.MaterialArticle)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new { x.IsActive, x.Priority });
    }
}
