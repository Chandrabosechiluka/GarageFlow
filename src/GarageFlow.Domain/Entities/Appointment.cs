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

    private void TransitionTo(AppointmentStatus next)
    {
        if (!CanTransitionTo(next))
            throw new DomainException($"Cannot move an appointment from {Status} to {next}.");

        Status = next;
    }
}
