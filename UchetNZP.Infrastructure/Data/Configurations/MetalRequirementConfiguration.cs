using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalRequirementConfiguration : IEntityTypeConfiguration<MetalRequirement>
{
    public void Configure(EntityTypeBuilder<MetalRequirement> builder)
    {
        builder.ToTable("MetalRequirements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequirementNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.RequirementDate)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(256);

        builder.HasOne(x => x.WipLaunch)
            .WithMany(x => x.MetalRequirements)
            .HasForeignKey(x => x.WipLaunchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Part)
            .WithMany(x => x.MetalRequirements)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RequirementNumber)
            .IsUnique();

        builder.HasIndex(x => x.RequirementDate);

        builder.HasIndex(x => x.WipLaunchId);
    }
}
