using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GarageFlow.Infrastructure.Persistence.Converters;

/// <summary>
/// SQL Server's datetime2 has no time zone, so EF Core returns DateTimeKind.Unspecified.
/// We always store UTC, so this converter stamps the Kind back on when reading.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
