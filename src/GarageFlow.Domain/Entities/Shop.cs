using GarageFlow.Domain.Common;

namespace GarageFlow.Domain.Entities;

public class Shop : BaseEntity
{
    public const string DefaultTimeZoneId = "Asia/Kolkata";

    private Shop()
    {
    }

    public Shop(string name, TimeOnly openingTime, TimeOnly closingTime, string timeZoneId = DefaultTimeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Shop name is required.");
        if (closingTime <= openingTime)
            throw new DomainException("Closing time must be after opening time.");
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new DomainException("A time zone is required.");

        FindTimeZone(timeZoneId.Trim()); // throws DomainException if the name is not a real time zone

        Name = name.Trim();
        OpeningTime = openingTime;
        ClosingTime = closingTime;
        TimeZoneId = timeZoneId.Trim();
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Opening and closing are LOCAL wall-clock times in <see cref="TimeZoneId"/>.</summary>
    public TimeOnly OpeningTime { get; private set; }
    public TimeOnly ClosingTime { get; private set; }

    /// <summary>IANA name such as "Asia/Kolkata". Appointments are stored in UTC, so the shop needs to know its own zone.</summary>
    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;

    /// <summary>True when the whole interval [start, end) falls inside working hours.</summary>
    public bool IsOpenFor(TimeOnly start, TimeOnly end) =>
        start < end && start >= OpeningTime && end <= ClosingTime;

    /// <summary>The opening and closing moments of one local calendar day, converted to UTC.</summary>
    public (DateTime StartUtc, DateTime EndUtc) GetOpenWindowUtc(DateOnly localDate)
    {
        var zone = GetTimeZone();
        return (LocalToUtc(localDate.ToDateTime(OpeningTime), zone), LocalToUtc(localDate.ToDateTime(ClosingTime), zone));
    }

    /// <summary>True when the whole UTC interval fits inside opening hours of the local day it starts on.</summary>
    public bool IsOpenForUtc(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            return false;

        var localDate = DateOnly.FromDateTime(ToLocalTime(startUtc));
        var (open, close) = GetOpenWindowUtc(localDate);
        return startUtc >= open && endUtc <= close;
    }

    public DateTime ToLocalTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), GetTimeZone());

    /// <summary>Local time together with its UTC offset, handy for showing "09:00 +05:30" to a user.</summary>
    public DateTimeOffset ToLocalDateTimeOffset(DateTime utc)
    {
        var zone = GetTimeZone();
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var local = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(asUtc, zone), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(asUtc));
    }

    private TimeZoneInfo GetTimeZone() => FindTimeZone(TimeZoneId);

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (id == DefaultTimeZoneId)
        {
            // Older Windows setups without IANA names know India by its Windows name.
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainException($"Unknown time zone '{id}'.");
        }
    }

    // A local time can fall into the "spring forward" gap and not exist at all; move it past the gap.
    private static DateTime LocalToUtc(DateTime local, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(local))
            local = local.AddHours(1);

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
