using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class CuttingPlanItemConfiguration : IEntityTypeConfiguration<CuttingPlanItem>
{
    public void Configure(EntityTypeBuilder<CuttingPlanItem> builder)
    {
        builder.ToTable("cutting_plan_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ItemType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Length)
            .HasPrecision(12, 3);

        builder.Property(x => x.Width)
            .HasPrecision(12, 3);

        builder.Property(x => x.Height)
            .HasPrecision(12, 3);

        builder.Property(x => x.PositionX)
            .HasPrecision(12, 3);

        builder.Property(x => x.PositionY)
            .HasPrecision(12, 3);

        builder.HasOne(x => x.CuttingPlan)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CuttingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CuttingPlanId, x.StockIndex, x.Sequence });
    }
}
