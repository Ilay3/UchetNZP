using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.ToTable("SystemParameters");

        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key)
            .HasMaxLength(128);

        builder.Property(x => x.DecimalValue)
            .HasPrecision(12, 4);

        builder.Property(x => x.TextValue)
            .HasMaxLength(512);

        builder.Property(x => x.Description)
            .HasMaxLength(512);

        builder.Property(x => x.UpdatedAt)
            .IsRequired();
    }
}
