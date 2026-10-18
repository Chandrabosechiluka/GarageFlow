using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Entities;

/// <summary>
/// A booked service visit. All status changes go through the methods below so the
/// state machine cannot be bypassed (private setter on Status).
/// </summary>
public class Appointment : BaseEntity
{
    // The single source of truth for which status changes are legal.
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Booked] = new[] { AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow },
        [AppointmentStatus.CheckedIn] = new[] { AppointmentStatus.InInspection, AppointmentStatus.Cancelled },
        [AppointmentStatus.InInspection] = new[] { AppointmentStatus.Completed },
        [AppointmentStatus.Completed] = Array.Empty<AppointmentStatus>(),
        [AppointmentStatus.Cancelled] = Array.Empty<AppointmentStatus>(),
        [AppointmentStatus.NoShow] = Array.Empty<AppointmentStatus>()
    };

    /// <summary>A Booked appointment cannot be cancelled or moved when it starts in less than this (an Admin can override).</summary>
    public static readonly TimeSpan MinimumChangeNotice = TimeSpan.FromHours(2);

    private readonly List<InspectionItem> _inspectionItems = new();

    private Appointment()
    {
    }

    public Guid ShopId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid ServiceTypeId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public Guid BayId { get; private set; }

    /// <summary>All times are stored in UTC.</summary>
    public DateTime StartTimeUtc { get; private set; }
    public DateTime EndTimeUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>When the reminder job told the customer about this appointment (null = not yet). Used so it happens once.</summary>
    public DateTime? ReminderSentUtc { get; private set; }

    /// <summary>Used later for optimistic concurrency (EF Core row version).</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<InspectionItem> InspectionItems => _inspectionItems;

    public static Appointment Book(
        Guid shopId,
        Guid customerId,
        Guid vehicleId,
        ServiceType service,
        Guid technicianId,
        Guid bayId,
        DateTime startUtc,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(service);

        ValidateStart(startUtc, nowUtc);

        return new Appointment
        {
            ShopId = shopId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ServiceTypeId = service.Id,
            TechnicianId = technicianId,
            BayId = bayId,
            StartTimeUtc = startUtc,
            EndTimeUtc = startUtc.AddMinutes(service.DurationMinutes),
            Status = AppointmentStatus.Booked
        };
    }

    /// <summary>
    /// The start-time rules on their own, so callers can check them early (before searching for a free technician)
    /// and still get exactly the same errors as <see cref="Book"/>.
    /// </summary>
    public static void ValidateStart(DateTime startUtc, DateTime nowUtc)
    {
        if (startUtc.Kind != DateTimeKind.Utc)
            throw new DomainException("Appointment times must be in UTC.");
        if (startUtc <= nowUtc)
            throw new DomainException("Appointment must start in the future.");
    }

    public bool CanTransitionTo(AppointmentStatus next) => AllowedTransitions[Status].Contains(next);

    public void CheckIn() => TransitionTo(AppointmentStatus.CheckedIn);

    public void StartInspection() => TransitionTo(AppointmentStatus.InInspection);

    public void Complete()
    {
        if (Status == AppointmentStatus.InInspection && _inspectionItems.Count == 0)
            throw new DomainException("At least one inspection item is required before completing.");

        TransitionTo(AppointmentStatus.Completed);
    }

    public void Cancel(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A cancellation reason is required.");

        TransitionTo(AppointmentStatus.Cancelled);
        CancellationReason = reason.Trim();
    }

    /// <summary>
    /// Cancel with the notice policy: a Booked appointment starting in under two hours can only be cancelled
    /// with <paramref name="allowLate"/> (an Admin decision). The older overload above has no policy.
    /// </summary>
    public void Cancel(string? reason, DateTime nowUtc, bool allowLate = false)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A cancellation reason is required.");
        EnsureEnoughNotice(nowUtc, allowLate, "cancelled");

        Cancel(reason);
    }

    /// <summary>
    /// Moves a Booked appointment to a new time (and possibly a new technician and bay).
    /// The caller has already checked that the new slot is free; this method guards the appointment's own rules.
    /// </summary>
    public void Reschedule(DateTime newStartUtc, int durationMinutes, Guid technicianId, Guid bayId, DateTime nowUtc, bool allowLate = false)
    {
        if (Status != AppointmentStatus.Booked)
            throw new DomainException($"Only a Booked appointment can be rescheduled (this one is {Status}).");
        if (durationMinutes <= 0)
            throw new DomainException("Service duration must be positive.");

        EnsureEnoughNotice(nowUtc, allowLate, "rescheduled");
        ValidateStart(newStartUtc, nowUtc);

        TechnicianId = technicianId;
        BayId = bayId;
        StartTimeUtc = newStartUtc;
        EndTimeUtc = newStartUtc.AddMinutes(durationMinutes);
        ReminderSentUtc = null; // a new time deserves a fresh reminder
    }

    /// <summary>True when the customer is late enough to count as a no-show: still Booked and the grace period has passed.</summary>
    public bool IsNoShowDue(DateTime nowUtc, TimeSpan grace) =>
        Status == AppointmentStatus.Booked && StartTimeUtc + grace <= nowUtc;

    public void MarkReminderSent(DateTime nowUtc)
    {
        if (Status != AppointmentStatus.Booked)
            throw new DomainException($"Only a Booked appointment gets a reminder (this one is {Status}).");

        ReminderSentUtc = nowUtc;
    }

    public void MarkNoShow() => TransitionTo(AppointmentStatus.NoShow);

    public InspectionItem AddInspectionItem(string name, InspectionCondition condition, string? notes)
    {
        if (Status != AppointmentStatus.InInspection)
            throw new DomainException("Inspection items can only be added while the appointment is in inspection.");

        var item = new InspectionItem(Id, name, condition, notes);
        _inspectionItems.Add(item);
        return item;
    }

    /// <summary>
    /// Time ranges are half-open [start, end): an appointment ending at 10:00 does not
    /// clash with one starting at 10:00.
    /// </summary>
    public bool OverlapsWith(DateTime startUtc, DateTime endUtc) =>
        StartTimeUtc < endUtc && startUtc < EndTimeUtc;

    private void EnsureEnoughNotice(DateTime nowUtc, bool allowLate, string verb)
    {
        if (!allowLate && Status == AppointmentStatus.Booked && StartTimeUtc - nowUtc < MinimumChangeNotice)
            throw new DomainException(
                $"A booking cannot be {verb} less than {MinimumChangeNotice.TotalHours:0} hours before it starts. Ask an admin to override.");
    }

    private void TransitionTo(AppointmentStatus next)
    {
        if (!CanTransitionTo(next))
            throw new DomainException($"Cannot move an appointment from {Status} to {next}.");

        Status = next;
    }
}
