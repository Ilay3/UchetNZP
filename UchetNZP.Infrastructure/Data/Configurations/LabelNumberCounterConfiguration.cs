using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UchetNZP.Domain.Entities;

namespace UchetNZP.Infrastructure.Data.Configurations;

public class LabelNumberCounterConfiguration : IEntityTypeConfiguration<LabelNumberCounter>
{
    public void Configure(EntityTypeBuilder<LabelNumberCounter> builder)
    {
        builder.ToTable("LabelNumberCounters");

        builder.HasKey(x => x.RootNumber);

        builder.Property(x => x.RootNumber)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.NextSuffix)
            .IsRequired();
    }
}
