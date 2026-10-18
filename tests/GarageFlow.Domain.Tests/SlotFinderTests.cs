using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using GarageFlow.Domain.Scheduling;

namespace GarageFlow.Domain.Tests;

public class SlotFinderTests
{
    // Shop is open 09:00-18:00 India time = 03:30-12:30 UTC. Service takes 60 minutes and needs Brakes.
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly DateTime Morning = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime Utc(int hour, int minute) => new(2026, 11, 3, hour, minute, 0, DateTimeKind.Utc);

    private readonly Shop _shop = new("Test Garage", new TimeOnly(9, 0), new TimeOnly(18, 0));
    private readonly ServiceType _brakes = new("Brake Inspection", 60, Skill.Brakes);
    private readonly Technician _alice;
    private readonly Technician _bob;
    private readonly Technician _tyreOnly;
    private readonly Bay _bay1;
    private readonly Bay _bay2;

    public SlotFinderTests()
    {
        _alice = new Technician(_shop.Id, "Alice", new[] { Skill.Brakes });
        _bob = new Technician(_shop.Id, "Bob", new[] { Skill.Brakes });
        _tyreOnly = new Technician(_shop.Id, "Carl", new[] { Skill.Tires });
        _bay1 = new Bay(_shop.Id, "Bay 1");
        _bay2 = new Bay(_shop.Id, "Bay 2");
    }

    private IReadOnlyList<AvailableSlot> Find(IReadOnlyCollection<BusyPeriod>? busy = null, DateTime? now = null, ServiceType? service = null) =>
        SlotFinder.FindSlots(
            _shop, service ?? _brakes,
            new[] { _alice, _bob, _tyreOnly },
            new[] { _bay1, _bay2 },
            busy ?? Array.Empty<BusyPeriod>(),
            Day,
            now ?? Morning);

    [Fact]
    public void EmptyDay_HasASlotEveryThirtyMinutesUntilTheLastOneThatFits()
    {
        var slots = Find();

        Assert.Equal(17, slots.Count);
        Assert.Equal(Utc(3, 30), slots[0].StartUtc);
        Assert.Equal(Utc(11, 30), slots[^1].StartUtc);
        Assert.Equal(Utc(12, 30), slots[^1].EndUtc); // ends exactly at closing time
    }

    [Fact]
    public void ATechnicianWithoutTheSkill_IsNeverOffered()
    {
        var slots = Find();

        Assert.All(slots, slot => Assert.DoesNotContain(_tyreOnly.Id, slot.TechnicianIds));
    }

    [Fact]
    public void FreeTechnicians_AreListedAlphabetically()
    {
        var first = Find()[0];

        Assert.Equal(new[] { _alice.Id, _bob.Id }, first.TechnicianIds);
        Assert.Equal(new[] { _bay1.Id, _bay2.Id }, first.BayIds);
    }

    [Fact]
    public void ABusyTechnicianAndBay_AreRemovedOnlyFromOverlappingSlots()
    {
        var busy = new[] { new BusyPeriod(_alice.Id, _bay1.Id, Utc(3, 30), Utc(4, 30)) };

        var slots = Find(busy);

        var at0330 = slots.Single(s => s.StartUtc == Utc(3, 30));
        Assert.Equal(new[] { _bob.Id }, at0330.TechnicianIds);
        Assert.Equal(new[] { _bay2.Id }, at0330.BayIds);

        var at0400 = slots.Single(s => s.StartUtc == Utc(4, 0)); // overlaps the last half hour of the busy period
        Assert.Equal(new[] { _bob.Id }, at0400.TechnicianIds);

        var at0430 = slots.Single(s => s.StartUtc == Utc(4, 30)); // starts exactly when the busy period ends
        Assert.Equal(new[] { _alice.Id, _bob.Id }, at0430.TechnicianIds);
        Assert.Equal(new[] { _bay1.Id, _bay2.Id }, at0430.BayIds);
    }

    [Fact]
    public void WhenEveryQualifiedTechnicianIsBusy_TheSlotDisappears()
    {
        var busy = new[]
        {
            new BusyPeriod(_alice.Id, _bay1.Id, Utc(3, 30), Utc(4, 30)),
            new BusyPeriod(_bob.Id, _bay2.Id, Utc(3, 30), Utc(4, 30))
        };

        var slots = Find(busy);

        Assert.DoesNotContain(slots, s => s.StartUtc == Utc(3, 30));
        Assert.DoesNotContain(slots, s => s.StartUtc == Utc(4, 0));
        Assert.Contains(slots, s => s.StartUtc == Utc(4, 30));
    }

    [Fact]
    public void WhenEveryBayIsBusy_TheSlotDisappearsEvenIfTechniciansAreFree()
    {
        // The unqualified technician occupies both bays, so Alice and Bob are free but there is nowhere to work.
        var busy = new[]
        {
            new BusyPeriod(_tyreOnly.Id, _bay1.Id, Utc(3, 30), Utc(4, 30)),
            new BusyPeriod(Guid.NewGuid(), _bay2.Id, Utc(3, 30), Utc(4, 30))
        };

        var slots = Find(busy);

        Assert.DoesNotContain(slots, s => s.StartUtc == Utc(3, 30));
    }

    [Fact]
    public void SlotsInThePast_AreNotOffered()
    {
        var slots = Find(now: Utc(5, 0));

        Assert.Equal(Utc(5, 30), slots[0].StartUtc); // 05:00 itself is not in the future
        Assert.Equal(13, slots.Count);
    }

    [Fact]
    public void AServiceLongerThanTheOpeningHours_HasNoSlots()
    {
        var marathon = new ServiceType("Full Restoration", 600, Skill.Brakes);

        Assert.Empty(Find(service: marathon));
    }

    [Fact]
    public void ShorterServices_FitMoreSlots()
    {
        var quick = new ServiceType("Quick Check", 30, Skill.Brakes);

        Assert.Equal(18, Find(service: quick).Count); // 09:00 ... 17:30
    }
}
