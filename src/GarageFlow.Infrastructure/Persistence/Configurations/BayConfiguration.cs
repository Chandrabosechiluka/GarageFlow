using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class BayConfiguration : IEntityTypeConfiguration<Bay>
{
    public void Configure(EntityTypeBuilder<Bay> builder)
    {
        builder.ToTable("Bays");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Name).IsRequired().HasMaxLength(100);

        builder.HasIndex(b => new { b.ShopId, b.Name }).IsUnique();

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(b => b.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
