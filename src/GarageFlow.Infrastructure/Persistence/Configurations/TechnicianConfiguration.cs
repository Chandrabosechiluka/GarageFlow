using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("Technicians");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);

        // The public Skills collection is not mapped; the private SkillsStorage property
        // (a comma-separated string) is what lands in the "Skills" column.
        builder.Ignore(t => t.Skills);
        builder.Property<string>("SkillsStorage")
            .HasColumnName("Skills")
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne<Shop>()
            .WithMany()
            .HasForeignKey(t => t.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
