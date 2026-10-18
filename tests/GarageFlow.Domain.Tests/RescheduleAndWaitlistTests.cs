using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Tests;

public class RescheduleAndCancelPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ServiceType OilChange = new("Oil change", 45, Skill.OilChange);

    private static Appointment BookedAt(DateTime start) =>
        Appointment.Book(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OilChange, Guid.NewGuid(), Guid.NewGuid(), start, Now);

    [Fact]
    public void Reschedule_MovesStartEndTechnicianAndBay()
    {
        var appointment = BookedAt(Now.AddDays(1));
        var technician = Guid.NewGuid();
        var bay = Guid.NewGuid();
        var newStart = Now.AddDays(2);

        appointment.Reschedule(newStart, 45, technician, bay, Now);

        Assert.Equal(newStart, appointment.StartTimeUtc);
        Assert.Equal(newStart.AddMinutes(45), appointment.EndTimeUtc);
        Assert.Equal(technician, appointment.TechnicianId);
        Assert.Equal(bay, appointment.BayId);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    [Fact]
    public void Reschedule_IsOnlyAllowedWhileBooked()
    {
        var appointment = BookedAt(Now.AddDays(1));
        appointment.CheckIn();

        Assert.Throws<DomainException>(() =>
            appointment.Reschedule(Now.AddDays(2), 45, Guid.NewGuid(), Guid.NewGuid(), Now));
    }

    [Fact]
    public void Reschedule_RejectsPastAndNonUtcTimes()
    {
        var appointment = BookedAt(Now.AddDays(1));

        Assert.Throws<DomainException>(() =>
            appointment.Reschedule(Now.AddMinutes(-5), 45, Guid.NewGuid(), Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() =>
            appointment.Reschedule(DateTime.SpecifyKind(Now.AddDays(2), DateTimeKind.Unspecified), 45, Guid.NewGuid(), Guid.NewGuid(), Now));
    }

    [Fact]
    public void Reschedule_LessThanTwoHoursBeforeStart_NeedsOverride()
    {
        var appointment = BookedAt(Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            appointment.Reschedule(Now.AddDays(1), 45, Guid.NewGuid(), Guid.NewGuid(), Now));

        appointment.Reschedule(Now.AddDays(1), 45, Guid.NewGuid(), Guid.NewGuid(), Now, allowLate: true);
        Assert.Equal(Now.AddDays(1), appointment.StartTimeUtc);
    }

    [Fact]
    public void Cancel_WithEnoughNotice_Works()
    {
        var appointment = BookedAt(Now.AddHours(3));

        appointment.Cancel("Customer called", Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Cancel_ExactlyTwoHoursBefore_Works()
    {
        var appointment = BookedAt(Now.AddHours(2));

        appointment.Cancel("Customer called", Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Cancel_LessThanTwoHoursBefore_IsRefusedUnlessOverridden()
    {
        var appointment = BookedAt(Now.AddMinutes(90));

        var error = Assert.Throws<DomainException>(() => appointment.Cancel("Changed my mind", Now));
        Assert.Contains("2 hours", error.Message);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);

        appointment.Cancel("Changed my mind", Now, allowLate: true);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Cancel_AfterCheckIn_IgnoresTheNoticeRule()
    {
        var appointment = BookedAt(Now.AddMinutes(30));
        appointment.CheckIn();

        appointment.Cancel("Car broke down", Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Cancel_StillNeedsAReason()
    {
        var appointment = BookedAt(Now.AddDays(1));

        Assert.Throws<DomainException>(() => appointment.Cancel("  ", Now));
    }
}

public class WaitlistEntryTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static WaitlistEntry NewEntry(DateTime? earliest = null, DateTime? latest = null) =>
        WaitlistEntry.Join(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            earliest ?? Now.AddDays(1), latest ?? Now.AddDays(3), Now);

    [Fact]
    public void Join_StartsWaiting()
    {
        var entry = NewEntry();

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.Null(entry.OfferedStartUtc);
        Assert.Equal(Now, entry.CreatedUtc);
    }

    [Fact]
    public void Join_RejectsBadWindows()
    {
        Assert.Throws<DomainException>(() => NewEntry(Now.AddDays(3), Now.AddDays(1)));          // backwards
        Assert.Throws<DomainException>(() => NewEntry(Now.AddDays(-2), Now.AddDays(-1)));        // already over
        Assert.Throws<DomainException>(() => NewEntry(Now.AddDays(1), Now.AddDays(40)));         // too long
        Assert.Throws<DomainException>(() => NewEntry(
            DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Unspecified), Now.AddDays(2)));    // not UTC
    }

    [Fact]
    public void Offer_MustBeInsideTheWindow()
    {
        var entry = NewEntry();

        Assert.Throws<DomainException>(() => entry.Offer(Now.AddDays(5), Now.AddDays(5).AddHours(1), Guid.NewGuid(), Now));

        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);
        Assert.Equal(WaitlistStatus.Offered, entry.Status);
        Assert.Equal(Now.AddDays(2), entry.OfferedStartUtc);
    }

    [Fact]
    public void Offer_IsOnlyPossibleWhileWaiting()
    {
        var entry = NewEntry();
        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);

        Assert.Throws<DomainException>(() => entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now));
    }

    [Fact]
    public void AcceptFlow_OfferedToAcceptingToFulfilled()
    {
        var entry = NewEntry();
        var slot = Now.AddDays(2);
        entry.Offer(slot, slot.AddHours(1), Guid.NewGuid(), Now);

        Assert.Equal(slot, entry.BeginAccept(Now));
        Assert.Equal(WaitlistStatus.Accepting, entry.Status);

        var appointmentId = Guid.NewGuid();
        entry.Fulfil(appointmentId);
        Assert.Equal(WaitlistStatus.Fulfilled, entry.Status);
        Assert.Equal(appointmentId, entry.AppointmentId);
    }

    [Fact]
    public void BeginAccept_WithoutAnOffer_Throws()
    {
        Assert.Throws<DomainException>(() => NewEntry().BeginAccept(Now));
    }

    [Fact]
    public void WithdrawOffer_ReturnsToWaiting()
    {
        var entry = NewEntry();
        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);
        entry.BeginAccept(Now);

        entry.WithdrawOffer();

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.Null(entry.OfferedStartUtc);
    }

    [Fact]
    public void Cancel_WorksWhileWaitingOrOffered_ButNotAfterFulfilled()
    {
        var waiting = NewEntry();
        waiting.Cancel();
        Assert.Equal(WaitlistStatus.Cancelled, waiting.Status);

        var done = NewEntry();
        done.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);
        done.BeginAccept(Now);
        done.Fulfil(Guid.NewGuid());
        Assert.Throws<DomainException>(() => done.Cancel());
    }

    [Fact]
    public void Offer_ExpiresAfterTheOfferLifetime()
    {
        var entry = NewEntry();
        var technician = Guid.NewGuid();
        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), technician, Now);

        Assert.Equal(Now + WaitlistEntry.OfferLifetime, entry.OfferExpiresUtc);
        Assert.Equal(technician, entry.OfferedTechnicianId);
        Assert.False(entry.IsOfferExpired(Now.AddMinutes(119)));
        Assert.True(entry.IsOfferExpired(Now + WaitlistEntry.OfferLifetime));
    }

    [Fact]
    public void Expire_OnlyWorksOnceTheTimeIsUp()
    {
        var entry = NewEntry();
        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);

        Assert.Throws<DomainException>(() => entry.Expire(Now.AddMinutes(30)));

        entry.Expire(Now.AddHours(3));
        Assert.Equal(WaitlistStatus.Expired, entry.Status);
        Assert.NotNull(entry.OfferedStartUtc); // kept as history
        Assert.Throws<DomainException>(() => entry.Cancel());
    }

    [Fact]
    public void BeginAccept_AfterTheOfferExpired_Throws()
    {
        var entry = NewEntry();
        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);

        var error = Assert.Throws<DomainException>(() => entry.BeginAccept(Now.AddHours(3)));

        Assert.Contains("expired", error.Message);
        Assert.Equal(WaitlistStatus.Offered, entry.Status);
    }

    [Fact]
    public void MarkOfferNotified_NeedsAnOpenOffer()
    {
        var entry = NewEntry();
        Assert.Throws<DomainException>(() => entry.MarkOfferNotified(Now));

        entry.Offer(Now.AddDays(2), Now.AddDays(2).AddHours(1), Guid.NewGuid(), Now);
        entry.MarkOfferNotified(Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), entry.OfferNotifiedUtc);
    }
}

public class AppointmentJobRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ServiceType OilChange = new("Oil change", 45, Skill.OilChange);

    private static Appointment BookedAt(DateTime start) =>
        Appointment.Book(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OilChange, Guid.NewGuid(), Guid.NewGuid(), start, Now);

    [Fact]
    public void IsNoShowDue_OnlyAfterTheGracePeriod_AndOnlyWhileBooked()
    {
        var grace = TimeSpan.FromMinutes(30);
        var appointment = BookedAt(Now.AddHours(1));

        Assert.False(appointment.IsNoShowDue(Now.AddHours(1).AddMinutes(29), grace));
        Assert.True(appointment.IsNoShowDue(Now.AddHours(1).AddMinutes(30), grace));

        appointment.CheckIn();
        Assert.False(appointment.IsNoShowDue(Now.AddDays(1), grace));
    }

    [Fact]
    public void MarkReminderSent_RecordsTheTime_AndRescheduleResetsIt()
    {
        var appointment = BookedAt(Now.AddDays(1));

        appointment.MarkReminderSent(Now);
        Assert.Equal(Now, appointment.ReminderSentUtc);

        appointment.Reschedule(Now.AddDays(2), 45, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Null(appointment.ReminderSentUtc);
    }

    [Fact]
    public void MarkReminderSent_RefusesNonBookedAppointments()
    {
        var appointment = BookedAt(Now.AddDays(1));
        appointment.CheckIn();

        Assert.Throws<DomainException>(() => appointment.MarkReminderSent(Now));
    }
}
