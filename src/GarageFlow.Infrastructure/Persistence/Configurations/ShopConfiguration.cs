using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("Shops");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.OpeningTime).IsRequired();
        builder.Property(s => s.ClosingTime).IsRequired();
    }
}
