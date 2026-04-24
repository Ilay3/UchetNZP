using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalIssueItemConfiguration : IEntityTypeConfiguration<MetalIssueItem>
{
    public void Configure(EntityTypeBuilder<MetalIssueItem> builder)
    {
        builder.ToTable("MetalIssueItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceCode)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.SourceQtyBefore)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.IssuedQty)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.RemainingQtyAfter)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.LineStatus)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.HasOne(x => x.MetalIssue)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.MetalIssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MetalReceiptItem)
            .WithMany(x => x.IssueItems)
            .HasForeignKey(x => x.MetalReceiptItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MetalIssueId, x.SortOrder });
        builder.HasIndex(x => x.MetalReceiptItemId);
    }
}
