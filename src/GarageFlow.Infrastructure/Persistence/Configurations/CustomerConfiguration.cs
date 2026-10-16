using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(30);

        builder.HasIndex(c => c.Email).IsUnique();

        // A customer owns their vehicles: deleting the customer deletes the vehicles.
        builder.HasMany(c => c.Vehicles)
            .WithOne()
            .HasForeignKey(v => v.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Read and write through the private _vehicles list so the aggregate keeps control of additions.
        builder.Navigation(c => c.Vehicles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
