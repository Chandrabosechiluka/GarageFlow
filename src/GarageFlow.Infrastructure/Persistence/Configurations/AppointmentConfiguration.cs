using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.StartTimeUtc).IsRequired().HasConversion(new UtcDateTimeConverter());
        builder.Property(a => a.EndTimeUtc).IsRequired().HasConversion(new UtcDateTimeConverter());

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.CancellationReason).HasMaxLength(500);

        // SQL Server bumps this value on every update; EF uses it to detect two people editing
        // the same appointment (optimistic concurrency, used properly from Day 7).
        builder.Property(a => a.RowVersion).IsRowVersion();

        // Restrict, not Cascade: deleting a customer, vehicle or technician must never silently
        // delete appointment history. It also avoids SQL Server's "multiple cascade paths" error,
        // because Customer -> Appointment and Customer -> Vehicle -> Appointment would both cascade.
        builder.HasOne<Shop>().WithMany().HasForeignKey(a => a.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(a => a.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Technician>().WithMany().HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bay>().WithMany().HasForeignKey(a => a.BayId).OnDelete(DeleteBehavior.Restrict);

        // Inspection items belong to the appointment and go with it.
        builder.HasMany(a => a.InspectionItems)
            .WithOne()
            .HasForeignKey(i => i.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.InspectionItems).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes for the slot-availability queries coming on Day 5: "who/what is busy in this time range?"
        builder.HasIndex(a => new { a.TechnicianId, a.StartTimeUtc });
        builder.HasIndex(a => new { a.BayId, a.StartTimeUtc });
        builder.HasIndex(a => new { a.ShopId, a.StartTimeUtc });
    }
}
