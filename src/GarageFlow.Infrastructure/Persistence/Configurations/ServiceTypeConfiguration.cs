using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("ServiceTypes");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
        builder.Property(s => s.DurationMinutes).IsRequired();

        // Enums are stored as readable text ("Brakes") instead of numbers.
        builder.Property(s => s.RequiredSkill).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}
