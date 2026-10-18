using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("WaitlistEntries");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.EarliestStartUtc).IsRequired().HasConversion(new UtcDateTimeConverter());
        builder.Property(w => w.LatestStartUtc).IsRequired().HasConversion(new UtcDateTimeConverter());
        builder.Property(w => w.CreatedUtc).IsRequired().HasConversion(new UtcDateTimeConverter());
        builder.Property(w => w.OfferedStartUtc).HasConversion(new UtcDateTimeConverter());
        builder.Property(w => w.OfferedEndUtc).HasConversion(new UtcDateTimeConverter());
        builder.Property(w => w.OfferExpiresUtc).HasConversion(new UtcDateTimeConverter());
        builder.Property(w => w.OfferNotifiedUtc).HasConversion(new UtcDateTimeConverter());

        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Two people pressing "accept" together: only one save can win (HTTP 409 for the other).
        builder.Property(w => w.RowVersion).IsRowVersion();

        // History matters, so nothing here cascades.
        builder.HasOne<Shop>().WithMany().HasForeignKey(w => w.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(w => w.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(w => w.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(w => w.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(w => w.AppointmentId).OnDelete(DeleteBehavior.Restrict);

        // "Oldest waiting entries for this shop" is the query run whenever a slot opens up.
        builder.HasIndex(w => new { w.ShopId, w.Status, w.CreatedUtc });
        builder.HasIndex(w => new { w.VehicleId, w.ServiceTypeId });

        // The expiry and notification jobs scan open offers.
        builder.HasIndex(w => new { w.Status, w.OfferExpiresUtc });
    }
}
