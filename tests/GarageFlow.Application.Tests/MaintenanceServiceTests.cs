using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace GarageFlow.Application.Tests;

public sealed class RecordingNotifier : INotificationSender
{
    public List<Notification> Sent { get; } = new();

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

public sealed class FailingNotifier : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken ct = default) =>
        throw new InvalidOperationException("The mail server is down.");
}

public class MaintenanceServiceTests
{
    private static readonly DateTime WindowStart = Scenario.NineAm.AddHours(-1);
    private static readonly DateTime WindowEnd = Scenario.NineAm.AddHours(1);

    /// <summary>The job service as it would be at a given moment, over the scenario's database.</summary>
    private static MaintenanceService Jobs(Scenario s, DateTime nowUtc, INotificationSender notifier) =>
        new(s.Db, new FixedClock(nowUtc), notifier, new MaintenanceSettings(), NullLogger<MaintenanceService>.Instance);

    private static WaitlistService WaitlistAt(Scenario s, DateTime nowUtc) =>
        new(s.Db, new FixedClock(nowUtc), s.Appointments());

    // ---------- no-shows ----------

    [Fact]
    public async Task NoShows_AreMarkedOnlyAfterTheGracePeriod()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        Assert.Equal(0, await Jobs(s, Scenario.NineAm.AddMinutes(29), new RecordingNotifier()).MarkNoShowsAsync());
        Assert.Equal(AppointmentStatus.Booked, (await s.Appointments().GetAsync(booked.Id)).Status);

        Assert.Equal(1, await Jobs(s, Scenario.NineAm.AddMinutes(30), new RecordingNotifier()).MarkNoShowsAsync());
        Assert.Equal(AppointmentStatus.NoShow, (await s.Appointments().GetAsync(booked.Id)).Status);
    }

    [Fact]
    public async Task NoShows_LeaveCheckedInAndAlreadyHandledAppointmentsAlone()
    {
        var s = new Scenario();
        var arrived = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await s.Appointments().CheckInAsync(arrived.Id);

        Assert.Equal(0, await Jobs(s, Scenario.NineAm.AddDays(1), new RecordingNotifier()).MarkNoShowsAsync());
        Assert.Equal(AppointmentStatus.CheckedIn, (await s.Appointments().GetAsync(arrived.Id)).Status);
    }

    [Fact]
    public async Task NoShows_RunningTwiceDoesNothingTheSecondTime()
    {
        var s = new Scenario();
        await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var jobs = Jobs(s, Scenario.NineAm.AddHours(2), new RecordingNotifier());

        Assert.Equal(1, await jobs.MarkNoShowsAsync());
        Assert.Equal(0, await jobs.MarkNoShowsAsync());
    }

    // ---------- reminders ----------

    [Fact]
    public async Task Reminders_AreSentWithin24Hours_OnceOnly()
    {
        var s = new Scenario();
        await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var notifier = new RecordingNotifier();

        Assert.Equal(0, await Jobs(s, Scenario.NineAm.AddHours(-25), notifier).SendRemindersAsync()); // too early
        Assert.Equal(1, await Jobs(s, Scenario.NineAm.AddHours(-23), notifier).SendRemindersAsync());
        Assert.Equal(0, await Jobs(s, Scenario.NineAm.AddHours(-22), notifier).SendRemindersAsync()); // already told

        var message = Assert.Single(notifier.Sent);
        Assert.Equal(s.Customer.Email, message.ToEmail);
        Assert.Contains("Brake Inspection", message.Subject);
        Assert.Contains(s.Shop.Name, message.Body);
    }

    [Fact]
    public async Task Reminders_SkipCancelledAppointments()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        await s.Appointments().CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));
        var notifier = new RecordingNotifier();

        Assert.Equal(0, await Jobs(s, Scenario.NineAm.AddHours(-23), notifier).SendRemindersAsync());
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Reminders_WhenSendingFails_AreNotMarkedAndGoOutNextTime()
    {
        var s = new Scenario();
        await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var now = Scenario.NineAm.AddHours(-23);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Jobs(s, now, new FailingNotifier()).SendRemindersAsync());

        var working = new RecordingNotifier();
        Assert.Equal(1, await Jobs(s, now.AddMinutes(1), working).SendRemindersAsync());
        Assert.Single(working.Sent);
    }

    [Fact]
    public async Task Reminders_AreSentAgainAfterTheAppointmentIsMoved()
    {
        var s = new Scenario();
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var notifier = new RecordingNotifier();
        var now = Scenario.NineAm.AddHours(-23);
        await Jobs(s, now, notifier).SendRemindersAsync();

        // Move it to the next morning (still within 24h of the later check, so the new time gets its own reminder).
        var moved = new AppointmentService(s.Db, new FixedClock(now), s.Gate);
        await moved.RescheduleAsync(booked.Id, new RescheduleAppointmentRequest(Scenario.NineAm.AddHours(1), null, null));

        Assert.Equal(1, await Jobs(s, now.AddMinutes(1), notifier).SendRemindersAsync());
        Assert.Equal(2, notifier.Sent.Count);
    }

    // ---------- waitlist offers: notify and expire ----------

    /// <summary>One booking, one waiting customer, then the booking is cancelled so the customer is offered the slot.</summary>
    private static async Task<WaitlistEntryResponse> OfferedScenarioAsync(Scenario s)
    {
        var booked = await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));
        var entry = await s.Waitlist().JoinAsync(s.JoinRequest(WindowStart, WindowEnd));
        await s.Appointments().CancelAsync(booked.Id, new CancelAppointmentRequest("Customer called"));
        return entry;
    }

    [Fact]
    public async Task Offers_AreAnnouncedOnce_WithTheDeadline()
    {
        var s = new Scenario();
        await OfferedScenarioAsync(s);
        var notifier = new RecordingNotifier();
        var jobs = Jobs(s, Scenario.Now, notifier);

        Assert.Equal(1, await jobs.NotifyOffersAsync());
        Assert.Equal(0, await jobs.NotifyOffersAsync());

        var message = Assert.Single(notifier.Sent);
        Assert.Equal(s.Customer.Email, message.ToEmail);
        Assert.Contains("slot", message.Subject);
    }

    [Fact]
    public async Task Offers_DoNotExpireEarly_ButDoAfterTheirLifetime()
    {
        var s = new Scenario();
        var entry = await OfferedScenarioAsync(s);

        Assert.Equal(0, await Jobs(s, Scenario.Now.AddHours(1), new RecordingNotifier()).ExpireOffersAsync());
        Assert.Equal(1, await Jobs(s, Scenario.Now.AddHours(3), new RecordingNotifier()).ExpireOffersAsync());

        var expired = (await s.Waitlist().ListAsync(WaitlistStatus.Expired)).Single();
        Assert.Equal(entry.Id, expired.Id);
    }

    [Fact]
    public async Task ExpiredOffer_PassesTheSlotToTheNextCustomerInLine()
    {
        var s = new Scenario();
        var first = await OfferedScenarioAsync(s);

        var second = new Customer("Second Customer", "second@example.com", "9000000002");
        var secondVehicle = second.AddVehicle("Maruti", "Swift", 2020, "TS02BB0002");
        s.Db.Customers.Add(second);
        await s.Db.SaveChangesAsync();
        var later = await WaitlistAt(s, Scenario.Now.AddMinutes(1))
            .JoinAsync(new JoinWaitlistRequest(second.Id, secondVehicle.Id, s.BrakeService.Id, WindowStart, WindowEnd));

        await Jobs(s, Scenario.Now.AddHours(3), new RecordingNotifier()).ExpireOffersAsync();

        var offered = (await s.Waitlist().ListAsync(WaitlistStatus.Offered)).Single();
        Assert.Equal(later.Id, offered.Id);
        Assert.Equal(Scenario.NineAm, offered.OfferedStartUtc);
        Assert.Equal(WaitlistStatus.Expired, (await s.Waitlist().ListAsync(WaitlistStatus.Expired)).Single().Status);
        Assert.NotEqual(first.Id, offered.Id);
    }

    [Fact]
    public async Task ExpiredOffer_IsNotPassedOn_WhenTheSlotWasBookedMeanwhile()
    {
        var s = new Scenario();
        await OfferedScenarioAsync(s);

        var second = new Customer("Second Customer", "second@example.com", "9000000002");
        var secondVehicle = second.AddVehicle("Maruti", "Swift", 2020, "TS02BB0002");
        s.Db.Customers.Add(second);
        await s.Db.SaveChangesAsync();
        await WaitlistAt(s, Scenario.Now.AddMinutes(1))
            .JoinAsync(new JoinWaitlistRequest(second.Id, secondVehicle.Id, s.BrakeService.Id, WindowStart, WindowEnd));

        // Walk-in customers take the technician at that time before the offer runs out.
        await s.Appointments().BookAsync(s.Request(s.Tech1, s.Bay1, Scenario.NineAm));

        await Jobs(s, Scenario.Now.AddHours(3), new RecordingNotifier()).ExpireOffersAsync();

        Assert.Empty(await s.Waitlist().ListAsync(WaitlistStatus.Offered));
        Assert.Single(await s.Waitlist().ListAsync(WaitlistStatus.Waiting));
    }

    [Fact]
    public async Task AnExpiredOffer_CannotBeAccepted_EvenBeforeTheJobHasRun()
    {
        var s = new Scenario();
        var entry = await OfferedScenarioAsync(s);

        await Assert.ThrowsAsync<DomainException>(() => WaitlistAt(s, Scenario.Now.AddHours(3)).AcceptOfferAsync(entry.Id));

        Assert.Single(await s.Waitlist().ListAsync(WaitlistStatus.Offered));
    }
}
