using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class WipLaunchOperationConfiguration : IEntityTypeConfiguration<WipLaunchOperation>
{
    public void Configure(EntityTypeBuilder<WipLaunchOperation> builder)
    {
        builder.ToTable("WipLaunchOperations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OpNumber)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(12, 3);

        builder.Property(x => x.Hours)
            .HasPrecision(12, 3);

        builder.Property(x => x.NormHours)
            .HasPrecision(12, 3);

        builder.HasIndex(x => new { x.WipLaunchId, x.OpNumber })
            .IsUnique();

        builder.HasOne(x => x.WipLaunch)
            .WithMany(x => x.Operations)
            .HasForeignKey(x => x.WipLaunchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Operation)
            .WithMany(x => x.WipLaunchOperations)
            .HasForeignKey(x => x.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Section)
            .WithMany(x => x.WipLaunchOperations)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PartRoute)
            .WithMany(x => x.WipLaunchOperations)
            .HasForeignKey(x => x.PartRouteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
