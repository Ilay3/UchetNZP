using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalRequirementPlanConfiguration : IEntityTypeConfiguration<MetalRequirementPlan>
{
    public void Configure(EntityTypeBuilder<MetalRequirementPlan> builder)
    {
        builder.ToTable("MetalRequirementPlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.BaseRequiredQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.AdjustedRequiredQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.PlannedQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.DeficitQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.CalculationComment)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.RecalculatedBy)
            .HasMaxLength(128);

        builder.HasOne(x => x.MetalRequirement)
            .WithMany(x => x.RequirementPlans)
            .HasForeignKey(x => x.MetalRequirementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MetalRequirementId)
            .IsUnique();
    }
}
