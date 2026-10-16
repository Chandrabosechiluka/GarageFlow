using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class InspectionItemConfiguration : IEntityTypeConfiguration<InspectionItem>
{
    public void Configure(EntityTypeBuilder<InspectionItem> builder)
    {
        builder.ToTable("InspectionItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Condition).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Notes).HasMaxLength(1000);
    }
}
