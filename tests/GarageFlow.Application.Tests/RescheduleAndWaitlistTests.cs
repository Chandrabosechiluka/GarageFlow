using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

public class RescheduleTests
{
    [Fact]
    public async Task Reschedule_MovesTheAppointment_AndKeepsTechnicianAndBayWhenFree()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech2, s.Bay2, Scenario.NineAm));

        var moved = await service.RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.AddHours(2), null, null));

        Assert.Equal(Scenario.NineAm.AddHours(2), moved.StartTimeUtc);
        Assert.Equal(Scenario.NineAm.AddHours(3), moved.EndTimeUtc);
        Assert.Equal(s.Tech2.Id, moved.TechnicianId);
        Assert.Equal(s.Bay2.Id, moved.BayId);
        Assert.Equal(AppointmentStatus.Booked, moved.Status);
    }

    [Fact]
    public async Task Reschedule_CanMoveIntoItsOwnOverlap()
    {
        // 09:00-10:00 moved to 09:30-10:30 overlaps ITSELF; it must not clash with its old self.
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        var moved = await service.RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.AddMinutes(30), null, null));

        Assert.Equal(Scenario.NineAm.AddMinutes(30), moved.StartTimeUtc);
    }

    [Fact]
    public async Task Reschedule_WhenCurrentTechnicianAndBayAreTaken_AssignsOthers()
    {
        var s = new Scenario();
        var service = s.Appointments();
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var later = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm.AddHours(2)));

        var moved = await service.RescheduleAsync(later.Id, new RescheduleAppointmentRequest(Scenario.NineAm, null, null));

        Assert.Equal(s.Tech2.Id, moved.TechnicianId);
        Assert.Equal(s.Bay2.Id, moved.BayId);
    }

    [Fact]
    public async Task Reschedule_WithAnExplicitBusyTechnician_IsAConflict()
    {
        var s = new Scenario();
        var service = s.Appointments();
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var other = await service.BookAsync(s.Request(s.Tech2, s.Bay2, Scenario.NineAm.AddHours(2)));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RescheduleAsync(other.Id, new RescheduleAppointmentRequest(Scenario.NineAm, s.Tech1.Id, null)));

        var unchanged = await service.GetAsync(other.Id);
        Assert.Equal(Scenario.NineAm.AddHours(2), unchanged.StartTimeUtc);
    }

    [Fact]
    public async Task Reschedule_WhenNobodyIsFree_IsAConflict()
    {
        var s = new Scenario();
        var service = s.Appointments();
        await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await service.BookAsync(s.Request(s.Tech2, s.Bay2, Scenario.NineAm));
        var third = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm.AddHours(2)));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RescheduleAsync(third.Id, new RescheduleAppointmentRequest(Scenario.NineAm, null, null)));
    }

    [Fact]
    public async Task Reschedule_OutsideOpeningHours_Throws()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        // 03:00 UTC is 08:30 in India: before the shop opens at 09:00.
        await Assert.ThrowsAsync<DomainException>(() =>
            service.RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.Date.AddHours(3), null, null)));
    }

    [Fact]
    public async Task Reschedule_OnlyWhileBooked()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await service.CheckInAsync(booked.Id);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.AddHours(2), null, null)));
    }

    [Fact]
    public async Task Reschedule_UnknownAppointment_IsNotFound()
    {
        var s = new Scenario();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.Appointments().RescheduleAsync(Guid.NewGuid(), new RescheduleAppointmentRequest(Scenario.NineAm, null, null)));
    }

    [Fact]
    public async Task Reschedule_WithinTwoHoursOfStart_NeedsTheAdminOverride()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var soon = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.Now.AddHours(1)));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RescheduleAsync(soon.Id, new RescheduleAppointmentRequest(Scenario.NineAm, null, null)));

        var moved = await service.RescheduleAsync(soon.Id, new RescheduleAppointmentRequest(Scenario.NineAm, null, null, AllowLate: true));
        Assert.Equal(Scenario.NineAm, moved.StartTimeUtc);
    }

    [Fact]
    public async Task Reschedule_GoesThroughTheBookingGate()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        s.Gate.Keys.Clear();

        await service.RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.AddHours(2), null, null));

        Assert.Equal($"booking:{s.Shop.Id}", Assert.Single(s.Gate.Keys));
    }
}

public class CancellationTests
{
    [Fact]
    public async Task Cancel_WithNoticeWorks_AndFreesTheSlot()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var booked = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        var cancelled = await service.CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);

        // The same technician and bay can be booked again for that time.
        var again = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        Assert.Equal(AppointmentStatus.Booked, again.Status);
    }

    [Fact]
    public async Task Cancel_LessThanTwoHoursBefore_IsRefusedUnlessAllowLate()
    {
        var s = new Scenario();
        var service = s.Appointments();
        var soon = await service.BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.Now.AddHours(1)));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CancelAsync(soon.Id, new CancelAppointmentRequest("Changed my mind")));
        Assert.Equal(AppointmentStatus.Booked, (await service.GetAsync(soon.Id)).Status);

        var cancelled = await service.CancelAsync(soon.Id, new CancelAppointmentRequest("Changed my mind", AllowLate: true));
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);
    }
}

public class WaitlistTests
{
    private static readonly DateTime WindowStart = Scenario.NineAm.AddHours(-1);
    private static readonly DateTime WindowEnd = Scenario.NineAm.AddHours(1);

    [Fact]
    public async Task Join_CreatesAWaitingEntry()
    {
        var s = new Scenario();

        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.Equal(s.Shop.Id, entry.ShopId);
    }

    [Fact]
    public async Task Join_TwiceForTheSameVehicleAndService_IsAConflict()
    {
        var s = new Scenario();
        var waitlist = s.Waitlist();
        await waitlist.JoinAsync(s.JoinRequest(WindowStart, WindowEnd));

        await Assert.ThrowsAsync<ConflictException>(() => waitlist.JoinAsync(s.JoinRequest(WindowStart, WindowEnd)));
    }

    [Fact]
    public async Task Join_WithAnotherCustomersVehicle_Throws()
    {
        var s = new Scenario();
        var request = s.JoinRequest(WindowStart, WindowEnd) with { CustomerId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() => s.Waitlist().JoinAsync(request));
    }

    [Fact]
    public async Task Cancelling_ABooking_OffersTheFreedSlotToTheWaitingCustomer()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));

        await s.Appointments().CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));

        var offered = (await s.Waitlist().ListAsync(WaitlistStatus.Offered)).Single();
        Assert.Equal(entry.Id, offered.Id);
        Assert.Equal(Scenario.NineAm, offered.OfferedStartUtc);
    }

    [Fact]
    public async Task Cancelling_ABooking_DoesNotOfferToSomeoneWhoseWindowMissesTheSlot()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await s.Waitlist().JoinAsync(s.JoinRequest(Scenario.NineAm.AddDays(1), Scenario.NineAm.AddDays(2)));

        await s.Appointments().CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));

        Assert.Empty(await s.Waitlist().ListAsync(WaitlistStatus.Offered));
        Assert.Single(await s.Waitlist().ListAsync(WaitlistStatus.Waiting));
    }

    [Fact]
    public async Task Rescheduling_ABooking_OffersTheOldSlot()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));

        await s.Appointments().RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.AddHours(2), null, null));

        var offered = (await s.Waitlist().ListAsync(WaitlistStatus.Offered)).Single();
        Assert.Equal(Scenario.NineAm, offered.OfferedStartUtc);
    }

    [Fact]
    public async Task AcceptOffer_BooksTheAppointment_AndFulfilsTheEntry()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));
        await s.Appointments().CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));

        var appointment = await s.Waitlist().AcceptOfferAsync(entry.Id);

        Assert.Equal(Scenario.NineAm, appointment.StartTimeUtc);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        var after = (await s.Waitlist().ListAsync(WaitlistStatus.Fulfilled)).Single();
        Assert.Equal(appointment.Id, after.AppointmentId);
    }

    [Fact]
    public async Task AcceptOffer_WithoutAnOffer_Throws()
    {
        var s = new Scenario();
        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));

        await Assert.ThrowsAsync<DomainException>(() => s.Waitlist().AcceptOfferAsync(entry.Id));
    }

    [Fact]
    public async Task AcceptOffer_WhenTheSlotWasTakenMeanwhile_ReturnsTheCustomerToWaiting()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));
        await s.Appointments().CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));

        // Someone else fills BOTH technicians before the customer answers.
        await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await s.Appointments().BookAsync(s.Request(s.Tech2, s.Bay2, Scenario.NineAm));

        await Assert.ThrowsAsync<ConflictException>(() => s.Waitlist().AcceptOfferAsync(entry.Id));

        var back = (await s.Waitlist().ListAsync(WaitlistStatus.Waiting)).Single();
        Assert.Equal(entry.Id, back.Id);
        Assert.Null(back.OfferedStartUtc);
    }

    [Fact]
    public async Task CancelEntry_RemovesItFromTheQueue()
    {
        var s = new Scenario();
        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));

        var cancelled = await s.Waitlist().CancelAsync(entry.Id);

        Assert.Equal(WaitlistStatus.Cancelled, cancelled.Status);
        Assert.Empty(await s.Waitlist().ListAsync(WaitlistStatus.Waiting));
    }
}
