using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalRequirementItemConfiguration : IEntityTypeConfiguration<MetalRequirementItem>
{
    public void Configure(EntityTypeBuilder<MetalRequirementItem> builder)
    {
        builder.ToTable("MetalRequirementItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NormPerUnit)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.TotalRequiredQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.TotalRequiredWeightKg)
            .HasPrecision(12, 3);

        builder.Property(x => x.SelectionSource)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.SelectionReason)
            .HasMaxLength(512);

        builder.Property(x => x.CandidateMaterials)
            .HasMaxLength(2048);

        builder.HasOne(x => x.MetalRequirement)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.MetalRequirementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MetalMaterial)
            .WithMany(x => x.RequirementItems)
            .HasForeignKey(x => x.MetalMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MetalRequirementId, x.MetalMaterialId })
            .IsUnique();
    }
}
