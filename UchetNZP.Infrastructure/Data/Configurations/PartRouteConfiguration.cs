using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class PartRouteConfiguration : IEntityTypeConfiguration<PartRoute>
{
    public void Configure(EntityTypeBuilder<PartRoute> builder)
    {
        builder.ToTable("PartRoutes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OpNumber)
            .IsRequired();

        builder.Property(x => x.NormHours)
            .HasPrecision(10, 3);

        builder.HasIndex(x => new { x.PartId, x.OpNumber })
            .IsUnique();

        builder.HasOne(x => x.Part)
            .WithMany(x => x.Routes)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Operation)
            .WithMany(x => x.PartRoutes)
            .HasForeignKey(x => x.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Section)
            .WithMany(x => x.PartRoutes)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
