using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalIssueConfiguration : IEntityTypeConfiguration<MetalIssue>
{
    public void Configure(EntityTypeBuilder<MetalIssue> builder)
    {
        builder.ToTable("MetalIssues");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IssueNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.IssueDate)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.CompletedBy)
            .HasMaxLength(128);

        builder.HasOne(x => x.MetalRequirement)
            .WithMany(x => x.Issues)
            .HasForeignKey(x => x.MetalRequirementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IssueNumber)
            .IsUnique();

        builder.HasIndex(x => x.MetalRequirementId);
    }
}
