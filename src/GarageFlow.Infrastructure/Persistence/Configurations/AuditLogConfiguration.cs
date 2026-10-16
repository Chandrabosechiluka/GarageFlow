using GarageFlow.Domain.Entities;
using GarageFlow.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(50);
        builder.Property(a => a.PerformedBy).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.TimestampUtc).IsRequired().HasConversion(new UtcDateTimeConverter());

        // "Show me the history of this appointment."
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
    }
}
