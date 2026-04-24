using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalRequirementPlanItemConfiguration : IEntityTypeConfiguration<MetalRequirementPlanItem>
{
    public void Configure(EntityTypeBuilder<MetalRequirementPlanItem> builder)
    {
        builder.ToTable("MetalRequirementPlanItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceCode)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.SourceSize)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.SourceUnit)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.SourceWeightKg)
            .HasPrecision(12, 3);

        builder.Property(x => x.PlannedUseQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.RemainingAfterQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.LineStatus)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.HasOne(x => x.MetalRequirementPlan)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.MetalRequirementPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MetalReceiptItem)
            .WithMany(x => x.RequirementPlanItems)
            .HasForeignKey(x => x.MetalReceiptItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MetalRequirementPlanId, x.SortOrder });
        builder.HasIndex(x => x.MetalReceiptItemId);
    }
}
