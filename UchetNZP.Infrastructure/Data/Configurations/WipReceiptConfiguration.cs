using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipReceiptConfiguration : IEntityTypeConfiguration<WipReceipt>
{
    public void Configure(EntityTypeBuilder<WipReceipt> builder)
    {
        builder.ToTable("WipReceipts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.OpNumber)
            .IsRequired();

        builder.Property(x => x.ReceiptDate)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.HasIndex(x => new { x.PartId, x.SectionId, x.OpNumber, x.ReceiptDate });

        builder.HasOne(x => x.Part)
            .WithMany(x => x.WipReceipts)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Section)
            .WithMany(x => x.WipReceipts)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
