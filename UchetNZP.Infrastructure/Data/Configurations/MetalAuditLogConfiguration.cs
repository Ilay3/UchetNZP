using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class MetalAuditLogConfiguration : IEntityTypeConfiguration<MetalAuditLog>
{
    public void Configure(EntityTypeBuilder<MetalAuditLog> builder)
    {
        builder.ToTable("MetalAuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventDate)
            .IsRequired();

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.DocumentNumber)
            .HasMaxLength(64);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.UserName)
            .HasMaxLength(128);

        builder.Property(x => x.PayloadJson)
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.EventDate);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.DocumentNumber);
    }
}
