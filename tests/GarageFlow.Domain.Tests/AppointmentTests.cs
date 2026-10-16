using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Tests;

public class AppointmentTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly ServiceType OilChange = new("Oil change", 45, Skill.OilChange);

    private static Appointment NewAppointment(DateTime? start = null) =>
        Appointment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            OilChange,
            Guid.NewGuid(), Guid.NewGuid(),
            start ?? Now.AddDays(1),
            Now);

    /// <summary>Drives a new appointment through the legal path to reach the requested status.</summary>
    private static Appointment InState(AppointmentStatus status)
    {
        var appointment = NewAppointment();

        switch (status)
        {
            case AppointmentStatus.Booked:
                break;
            case AppointmentStatus.CheckedIn:
                appointment.CheckIn();
                break;
            case AppointmentStatus.InInspection:
                appointment.CheckIn();
                appointment.StartInspection();
                break;
            case AppointmentStatus.Completed:
                appointment.CheckIn();
                appointment.StartInspection();
                appointment.AddInspectionItem("Brake pads", InspectionCondition.Good, null);
                appointment.Complete();
                break;
            case AppointmentStatus.Cancelled:
                appointment.Cancel("Customer request");
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow();
                break;
        }

        return appointment;
    }

    [Fact]
    public void Book_SetsEndTimeFromServiceDuration_AndStartsAsBooked()
    {
        var start = Now.AddDays(1);

        var appointment = NewAppointment(start);

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Equal(start, appointment.StartTimeUtc);
        Assert.Equal(start.AddMinutes(45), appointment.EndTimeUtc);
        Assert.Equal(OilChange.Id, appointment.ServiceTypeId);
    }

    [Fact]
    public void Book_InThePast_Throws()
    {
        var past = Now.AddHours(-1);

        Assert.Throws<DomainException>(() => NewAppointment(past));
    }

    [Fact]
    public void Book_WithNonUtcTime_Throws()
    {
        var local = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() => NewAppointment(local));
    }

    [Fact]
    public void HappyPath_BookedToCompleted()
    {
        var appointment = NewAppointment();

        appointment.CheckIn();
        appointment.StartInspection();
        appointment.AddInspectionItem("Oil level", InspectionCondition.NeedsAttention, "Low");
        appointment.Complete();

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Single(appointment.InspectionItems);
    }

    [Theory]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.CheckedIn, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Cancelled, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.NoShow, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.InInspection, false)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Completed, false)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.InInspection, true)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, true)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.NoShow, false)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.Completed, false)]
    [InlineData(AppointmentStatus.InInspection, AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.InInspection, AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.CheckedIn, false)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.CheckedIn, false)]
    public void CanTransitionTo_FollowsTheStateMachine(AppointmentStatus from, AppointmentStatus to, bool expected)
    {
        var appointment = InState(from);

        Assert.Equal(expected, appointment.CanTransitionTo(to));
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void CheckIn_FromTerminalStatus_Throws(AppointmentStatus terminal)
    {
        var appointment = InState(terminal);

        Assert.Throws<DomainException>(() => appointment.CheckIn());
    }

    [Fact]
    public void MarkNoShow_AfterCheckIn_Throws()
    {
        var appointment = InState(AppointmentStatus.CheckedIn);

        Assert.Throws<DomainException>(() => appointment.MarkNoShow());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_WithoutReason_Throws(string? reason)
    {
        var appointment = NewAppointment();

        Assert.Throws<DomainException>(() => appointment.Cancel(reason));
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    [Fact]
    public void Cancel_StoresReasonAndChangesStatus()
    {
        var appointment = NewAppointment();

        appointment.Cancel("  Customer rescheduled  ");

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal("Customer rescheduled", appointment.CancellationReason);
    }

    [Fact]
    public void Complete_WithoutInspectionItems_Throws()
    {
        var appointment = InState(AppointmentStatus.InInspection);

        Assert.Throws<DomainException>(() => appointment.Complete());
        Assert.Equal(AppointmentStatus.InInspection, appointment.Status);
    }

    [Fact]
    public void AddInspectionItem_BeforeInspectionStarts_Throws()
    {
        var appointment = InState(AppointmentStatus.CheckedIn);

        Assert.Throws<DomainException>(() =>
            appointment.AddInspectionItem("Tyres", InspectionCondition.Good, null));
    }

    // Existing appointment runs 09:00-09:45 tomorrow. Offsets are minutes from its start.
    [Theory]
    [InlineData(-30, 30, false)] // 08:30-09:00 touches the start, no overlap
    [InlineData(-30, 31, true)]  // 08:30-09:01 overlaps by one minute
    [InlineData(10, 10, true)]   // fully inside
    [InlineData(44, 30, true)]   // starts one minute before it ends
    [InlineData(45, 30, false)]  // starts exactly when it ends
    public void OverlapsWith_UsesHalfOpenIntervals(int startOffsetMinutes, int durationMinutes, bool expected)
    {
        var appointment = NewAppointment();
        var start = appointment.StartTimeUtc.AddMinutes(startOffsetMinutes);
        var end = start.AddMinutes(durationMinutes);

        Assert.Equal(expected, appointment.OverlapsWith(start, end));
    }
}
