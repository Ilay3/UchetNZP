using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipBalanceCleanupStageItemConfiguration : IEntityTypeConfiguration<WipBalanceCleanupStageItem>
{
    public void Configure(EntityTypeBuilder<WipBalanceCleanupStageItem> builder)
    {
        builder.ToTable("WipBalanceCleanupStageItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PreviousQuantity)
            .HasPrecision(12, 3);

        builder.HasIndex(x => new { x.JobId, x.WipBalanceId })
            .IsUnique();

        builder.HasOne(x => x.Balance)
            .WithMany()
            .HasForeignKey(x => x.WipBalanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
