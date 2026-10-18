using GarageFlow.Application.Common;
using GarageFlow.Application.Services;

namespace GarageFlow.Application.Tests;

public class AvailabilityServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static AvailabilityService ServiceFor(Scenario s) => new(s.Db, s.Clock);

    [Fact]
    public async Task EmptyDay_OffersEveryHalfHourFromOpeningToTheLastPossibleStart()
    {
        var s = new Scenario();

        var result = await ServiceFor(s).GetAsync(s.BrakeService.Id, Day, null, null);

        // Open 09:00-18:00 local, 60-minute service, every 30 minutes: 09:00 ... 17:00 = 17 slots.
        Assert.Equal(17, result.Slots.Count);
        Assert.Equal("Asia/Kolkata", result.TimeZoneId);
        Assert.Equal(60, result.DurationMinutes);

        var first = result.Slots[0];
        Assert.Equal(new DateTime(2026, 11, 3, 3, 30, 0, DateTimeKind.Utc), first.StartUtc); // 09:00 local
        Assert.Equal(9, first.StartLocal.Hour);
        Assert.Equal(TimeSpan.FromHours(5.5), first.StartLocal.Offset);
        Assert.Equal(s.Tech1.Id, first.SuggestedTechnicianId);
        Assert.Equal(s.Bay1.Id, first.SuggestedBayId);
    }

    [Fact]
    public async Task ABookedSlot_NoLongerOffersTheBusyTechnicianAndBay()
    {
        var s = new Scenario();
        await new AppointmentService(s.Db, s.Clock).BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        var result = await ServiceFor(s).GetAsync(s.BrakeService.Id, Day, null, null);

        var slot = Assert.Single(result.Slots, x => x.StartUtc == Scenario.NineAm);
        Assert.Equal(new[] { s.Tech2.Id }, slot.AvailableTechnicianIds);
        Assert.Equal(new[] { s.Bay2.Id }, slot.AvailableBayIds);
    }

    [Fact]
    public async Task FilteringByTechnician_OnlyOffersThatTechnician()
    {
        var s = new Scenario();

        var result = await ServiceFor(s).GetAsync(s.BrakeService.Id, Day, null, s.Tech2.Id);

        Assert.NotEmpty(result.Slots);
        Assert.All(result.Slots, slot => Assert.Equal(new[] { s.Tech2.Id }, slot.AvailableTechnicianIds));
    }

    [Fact]
    public async Task ADayInThePast_HasNoSlots()
    {
        var s = new Scenario();

        var result = await ServiceFor(s).GetAsync(s.BrakeService.Id, new DateOnly(2026, 10, 1), null, null);

        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task UnknownService_ThrowsNotFound()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(s).GetAsync(Guid.NewGuid(), Day, null, null));
    }

    [Fact]
    public async Task UnknownTechnicianFilter_ThrowsNotFound()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(s).GetAsync(s.BrakeService.Id, Day, null, Guid.NewGuid()));
    }
}
