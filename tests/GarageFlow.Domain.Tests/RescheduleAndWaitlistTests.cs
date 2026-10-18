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

        Assert.Throws<DomainException>(() => entry.Offer(Now.AddDays(5)));

        entry.Offer(Now.AddDays(2));
        Assert.Equal(WaitlistStatus.Offered, entry.Status);
        Assert.Equal(Now.AddDays(2), entry.OfferedStartUtc);
    }

    [Fact]
    public void Offer_IsOnlyPossibleWhileWaiting()
    {
        var entry = NewEntry();
        entry.Offer(Now.AddDays(2));

        Assert.Throws<DomainException>(() => entry.Offer(Now.AddDays(2)));
    }

    [Fact]
    public void AcceptFlow_OfferedToAcceptingToFulfilled()
    {
        var entry = NewEntry();
        var slot = Now.AddDays(2);
        entry.Offer(slot);

        Assert.Equal(slot, entry.BeginAccept());
        Assert.Equal(WaitlistStatus.Accepting, entry.Status);

        var appointmentId = Guid.NewGuid();
        entry.Fulfil(appointmentId);
        Assert.Equal(WaitlistStatus.Fulfilled, entry.Status);
        Assert.Equal(appointmentId, entry.AppointmentId);
    }

    [Fact]
    public void BeginAccept_WithoutAnOffer_Throws()
    {
        Assert.Throws<DomainException>(() => NewEntry().BeginAccept());
    }

    [Fact]
    public void WithdrawOffer_ReturnsToWaiting()
    {
        var entry = NewEntry();
        entry.Offer(Now.AddDays(2));
        entry.BeginAccept();

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
        done.Offer(Now.AddDays(2));
        done.BeginAccept();
        done.Fulfil(Guid.NewGuid());
        Assert.Throws<DomainException>(() => done.Cancel());
    }
}
