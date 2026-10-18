using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;

namespace GarageFlow.Domain.Tests;

public class ShopTimeZoneTests
{
    private static Shop IndianShop() => new("Chennai Garage", new TimeOnly(9, 0), new TimeOnly(18, 0), "Asia/Kolkata");

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void OpenWindow_ConvertsLocalOpeningHoursToUtc()
    {
        var (start, end) = IndianShop().GetOpenWindowUtc(new DateOnly(2026, 11, 3));

        Assert.Equal(Utc(2026, 11, 3, 3, 30), start); // 09:00 at UTC+05:30
        Assert.Equal(Utc(2026, 11, 3, 12, 30), end);  // 18:00 at UTC+05:30
    }

    [Fact]
    public void OpenWindow_FollowsDaylightSavingTime()
    {
        var shop = new Shop("New York Garage", new TimeOnly(9, 0), new TimeOnly(17, 0), "America/New_York");

        // US clocks go back on 1 November 2026: the same 09:00 local is 13:00 UTC before and 14:00 UTC after.
        Assert.Equal(Utc(2026, 10, 31, 13, 0), shop.GetOpenWindowUtc(new DateOnly(2026, 10, 31)).StartUtc);
        Assert.Equal(Utc(2026, 11, 2, 14, 0), shop.GetOpenWindowUtc(new DateOnly(2026, 11, 2)).StartUtc);
    }

    [Fact]
    public void IsOpenForUtc_InsideOpeningHours_IsTrue()
    {
        Assert.True(IndianShop().IsOpenForUtc(Utc(2026, 11, 3, 4, 0), Utc(2026, 11, 3, 5, 0)));
    }

    [Fact]
    public void IsOpenForUtc_ExactlyFromOpeningToClosing_IsTrue()
    {
        Assert.True(IndianShop().IsOpenForUtc(Utc(2026, 11, 3, 3, 30), Utc(2026, 11, 3, 12, 30)));
    }

    [Fact]
    public void IsOpenForUtc_StartingBeforeOpening_IsFalse()
    {
        Assert.False(IndianShop().IsOpenForUtc(Utc(2026, 11, 3, 3, 0), Utc(2026, 11, 3, 4, 0)));
    }

    [Fact]
    public void IsOpenForUtc_EndingAfterClosing_IsFalse()
    {
        Assert.False(IndianShop().IsOpenForUtc(Utc(2026, 11, 3, 12, 0), Utc(2026, 11, 3, 13, 0)));
    }

    [Fact]
    public void ToLocalDateTimeOffset_ShowsLocalTimeWithItsOffset()
    {
        var local = IndianShop().ToLocalDateTimeOffset(Utc(2026, 11, 3, 3, 30));

        Assert.Equal(9, local.Hour);
        Assert.Equal(0, local.Minute);
        Assert.Equal(TimeSpan.FromHours(5.5), local.Offset);
    }

    [Fact]
    public void UnknownTimeZone_IsRejected()
    {
        Assert.Throws<DomainException>(() => new Shop("Nowhere", new TimeOnly(9, 0), new TimeOnly(17, 0), "Mars/Olympus_Mons"));
    }

    [Fact]
    public void DefaultTimeZone_IsIndia()
    {
        Assert.Equal("Asia/Kolkata", new Shop("Default", new TimeOnly(9, 0), new TimeOnly(17, 0)).TimeZoneId);
    }
}
