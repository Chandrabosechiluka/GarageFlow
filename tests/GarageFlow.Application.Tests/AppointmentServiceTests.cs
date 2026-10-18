using GarageFlow.Application.Common;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

public class AppointmentServiceTests
{
    private static AppointmentService ServiceFor(Scenario s) => new(s.Db, s.Clock);

    [Fact]
    public async Task Book_ValidRequest_CreatesBookedAppointmentEndingAfterServiceDuration()
    {
        var s = new Scenario();

        var result = await ServiceFor(s).BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        Assert.Equal(AppointmentStatus.Booked, result.Status);
        Assert.Equal(Scenario.NineAm.AddMinutes(60), result.EndTimeUtc);
        Assert.Equal(1, await s.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Book_TechnicianWithoutTheRequiredSkill_IsRejected()
    {
        var s = new Scenario();
        var tyreOnly = new Technician(s.Shop.Id, "Tyre Only", new[] { Skill.Tires });
        s.Db.Technicians.Add(tyreOnly);
        await s.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            ServiceFor(s).BookAsync(s.Request(tyreOnly, s.Bay1, Scenario.NineAm)));
    }

    [Fact]
    public async Task Book_VehicleOfAnotherCustomer_IsRejected()
    {
        var s = new Scenario();
        var other = new Customer("Other Person", "other@example.com", "9000000002");
        var otherVehicle = other.AddVehicle("Tata", "Nexon", 2022, "TS02BB0002");
        s.Db.Customers.Add(other);
        await s.Db.SaveChangesAsync();

        var request = s.Request(s.Tech1, s.Bay1, Scenario.NineAm) with { VehicleId = otherVehicle.Id };

        await Assert.ThrowsAsync<DomainException>(() => ServiceFor(s).BookAsync(request));
    }

    [Fact]
    public async Task Book_StartInThePast_IsRejected()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<DomainException>(() =>
            ServiceFor(s).BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.Now.AddHours(-1))));
    }

    [Fact]
    public async Task Book_StartTimeNotInUtc_IsRejected()
    {
        var s = new Scenario();
        var unspecified = DateTime.SpecifyKind(Scenario.NineAm, DateTimeKind.Unspecified);

        await Assert.ThrowsAsync<DomainException>(() =>
            ServiceFor(s).BookAsync(s.Request(s.Tech1, s.Bay1, unspecified)));
    }

    [Fact]
    public async Task Book_UnknownCustomer_ThrowsNotFound()
    {
        var s = new Scenario();
        var request = s.Request(s.Tech1, s.Bay1, Scenario.NineAm) with { CustomerId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(s).BookAsync(request));
    }

    [Fact]
    public async Task Book_SameTechnicianOverlapping_IsAConflict()
    {
        var s = new Scenario();
        var service = ServiceFor(s);
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        // Different bay, but Tech1 is busy 09:00-10:00 and this starts at 09:30.
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.BookAsync(s.Request(s.Tech1, s.Bay2, Scenario.NineAm.AddMinutes(30))));
    }

    [Fact]
    public async Task Book_SameBayOverlapping_IsAConflict()
    {
        var s = new Scenario();
        var service = ServiceFor(s);
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        // Different technician, but Bay1 is busy 09:00-10:00.
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.BookAsync(s.Request(s.Tech2, s.Bay1, Scenario.NineAm.AddMinutes(30))));
    }

    [Fact]
    public async Task Book_DifferentTechnicianAndBayAtTheSameTime_Succeeds()
    {
        var s = new Scenario();
        var service = ServiceFor(s);
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        var second = await service.BookAsync(s.Request(s.Tech2, s.Bay2, Scenario.NineAm));

        Assert.Equal(AppointmentStatus.Booked, second.Status);
        Assert.Equal(2, await s.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Book_BackToBack_DoesNotClash()
    {
        var s = new Scenario();
        var service = ServiceFor(s);
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        // The first ends exactly at 10:00; half-open intervals mean 10:00 is free again.
        var second = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm.AddMinutes(60)));

        Assert.Equal(Scenario.NineAm.AddMinutes(60), second.StartTimeUtc);
    }

    [Fact]
    public async Task Book_AfterTheFirstWasCancelled_TheSlotIsFreeAgain()
    {
        var s = new Scenario();
        var service = ServiceFor(s);

        // An already-cancelled appointment in the same slot (built with the domain, inserted directly).
        var cancelled = Appointment.Book(
            s.Shop.Id, s.Customer.Id, s.Vehicle.Id, s.BrakeService, s.Tech1.Id, s.Bay1.Id, Scenario.NineAm, Scenario.Now);
        cancelled.Cancel("Customer changed plans");
        s.Db.Appointments.Add(cancelled);
        await s.Db.SaveChangesAsync();

        var again = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        Assert.Equal(AppointmentStatus.Booked, again.Status);
    }

    // ---- Day 5: automatic assignment and opening hours -------------------------------------------------------

    [Fact]
    public async Task Book_WithoutTechnicianAndBay_AssignsTheFirstFreeOnes()
    {
        var s = new Scenario();

        var result = await ServiceFor(s).BookAsync(s.RequestAuto(Scenario.NineAm));

        // Assignment is deterministic: alphabetical, so "Tech One" and "Bay 1" win.
        Assert.Equal(s.Tech1.Id, result.TechnicianId);
        Assert.Equal(s.Bay1.Id, result.BayId);
    }

    [Fact]
    public async Task Book_AutoAssign_SkipsTheBusyTechnicianAndBay()
    {
        var s = new Scenario();
        var service = ServiceFor(s);
        await service.BookAsync(s.RequestAuto(Scenario.NineAm));

        var second = await service.BookAsync(s.RequestAuto(Scenario.NineAm));

        Assert.Equal(s.Tech2.Id, second.TechnicianId);
        Assert.Equal(s.Bay2.Id, second.BayId);
    }

    [Fact]
    public async Task Book_AutoAssign_WhenEveryoneIsBusy_IsAConflict()
    {
        var s = new Scenario();
        var service = ServiceFor(s);
        await service.BookAsync(s.RequestAuto(Scenario.NineAm));
        await service.BookAsync(s.RequestAuto(Scenario.NineAm));

        await Assert.ThrowsAsync<ConflictException>(() => service.BookAsync(s.RequestAuto(Scenario.NineAm)));
    }

    [Fact]
    public async Task Book_AutoAssign_OnlyPicksTechniciansWithTheRequiredSkill()
    {
        var s = new Scenario();
        // Alphabetically first, but cannot do brakes: must never be chosen.
        var tyreOnly = new Technician(s.Shop.Id, "Aaron Tyres", new[] { Skill.Tires });
        s.Db.Technicians.Add(tyreOnly);
        await s.Db.SaveChangesAsync();

        var result = await ServiceFor(s).BookAsync(s.RequestAuto(Scenario.NineAm));

        Assert.NotEqual(tyreOnly.Id, result.TechnicianId);
    }

    [Fact]
    public async Task Book_BeforeTheShopOpens_IsRejected()
    {
        var s = new Scenario();
        // 02:00 UTC is 07:30 in India; the shop opens at 09:00 local (03:30 UTC).
        var tooEarly = new DateTime(2026, 11, 3, 2, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<DomainException>(() => ServiceFor(s).BookAsync(s.RequestAuto(tooEarly)));
    }

    [Fact]
    public async Task Book_RunningPastClosingTime_IsRejected()
    {
        var s = new Scenario();
        // 12:00 UTC is 17:30 local; a 60-minute service would end at 18:30, after the 18:00 closing.
        var tooLate = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<DomainException>(() => ServiceFor(s).BookAsync(s.RequestAuto(tooLate)));
    }

    [Fact]
    public async Task Book_EndingExactlyAtClosingTime_IsAllowed()
    {
        var s = new Scenario();
        // 11:30 UTC is 17:00 local; 60 minutes later is exactly 18:00.
        var lastSlot = new DateTime(2026, 11, 3, 11, 30, 0, DateTimeKind.Utc);

        var result = await ServiceFor(s).BookAsync(s.RequestAuto(lastSlot));

        Assert.Equal(lastSlot.AddMinutes(60), result.EndTimeUtc);
    }
}
