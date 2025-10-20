using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipBalanceAdjustmentConfiguration : IEntityTypeConfiguration<WipBalanceAdjustment>
{
    public void Configure(EntityTypeBuilder<WipBalanceAdjustment> builder)
    {
        builder.ToTable("WipBalanceAdjustments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OpNumber)
            .IsRequired();

        builder.Property(x => x.PreviousQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.NewQuantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.Delta)
            .HasPrecision(12, 3);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.WipBalanceId);

        builder.HasOne(x => x.Balance)
            .WithMany()
            .HasForeignKey(x => x.WipBalanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Section)
            .WithMany()
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
