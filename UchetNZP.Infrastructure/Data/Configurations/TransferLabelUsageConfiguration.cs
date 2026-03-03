using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class TransferLabelUsageConfiguration : IEntityTypeConfiguration<TransferLabelUsage>
{
    public void Configure(EntityTypeBuilder<TransferLabelUsage> builder)
    {
        builder.ToTable("TransferLabelUsages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Qty)
            .HasPrecision(12, 3);

        builder.Property(x => x.ScrapQty)
            .HasPrecision(12, 3);

        builder.Property(x => x.RemainingBefore)
            .HasPrecision(12, 3);

        builder.HasCheckConstraint("CK_TransferLabelUsages_Qty_NonNegative", "\"Qty\" >= 0");
        builder.HasCheckConstraint("CK_TransferLabelUsages_ScrapQty_NonNegative", "\"ScrapQty\" >= 0");
        builder.HasCheckConstraint("CK_TransferLabelUsages_RemainingBefore_NonNegative", "\"RemainingBefore\" >= 0");
        builder.HasCheckConstraint("CK_TransferLabelUsages_Consumption_WithinRemaining", "(\"Qty\" + \"ScrapQty\") <= \"RemainingBefore\"");

        builder.HasOne(x => x.Transfer)
            .WithMany(x => x.LabelUsages)
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FromLabel)
            .WithMany(x => x.TransferUsagesAsSource)
            .HasForeignKey(x => x.FromLabelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedToLabel)
            .WithMany(x => x.TransferUsagesAsCreated)
            .HasForeignKey(x => x.CreatedToLabelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.FromLabelId);
    }
}
