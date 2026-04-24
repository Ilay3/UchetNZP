using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class CuttingPlanConfiguration : IEntityTypeConfiguration<CuttingPlan>
{
    public void Configure(EntityTypeBuilder<CuttingPlan> builder)
    {
        builder.ToTable("cutting_plan");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRequired();

        builder.Property(x => x.InputHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ParametersJson)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.UtilizationPercent)
            .HasPrecision(8, 3)
            .IsRequired();

        builder.Property(x => x.WastePercent)
            .HasPrecision(8, 3)
            .IsRequired();

        builder.Property(x => x.BusinessResidual)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.ScrapResidual)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsCurrent)
            .IsRequired();

        builder.HasOne(x => x.MetalRequirement)
            .WithMany(x => x.CuttingPlans)
            .HasForeignKey(x => x.MetalRequirementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.MetalRequirementId, x.Kind, x.Version })
            .IsUnique();

        builder.HasIndex(x => new { x.MetalRequirementId, x.Kind, x.IsCurrent });
    }
}
