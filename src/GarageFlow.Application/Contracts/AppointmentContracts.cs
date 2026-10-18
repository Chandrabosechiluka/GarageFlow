using System.ComponentModel.DataAnnotations;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Application.Contracts;

/// <summary>
/// StartTimeUtc must be sent with a "Z" (for example 2026-11-03T09:00:00Z).
/// Leave TechnicianId and/or BayId empty and the system assigns a free, qualified one for you
/// (see GET /api/availability for the times that can be booked).
/// </summary>
public record BookAppointmentRequest(
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid? TechnicianId,
    Guid? BayId,
    DateTime StartTimeUtc);

/// <summary>
/// AllowLate lifts the "at least 2 hours notice" rule. Only an Admin may set it (the API checks the role).
/// </summary>
public record CancelAppointmentRequest([property: Required, StringLength(500)] string Reason, bool AllowLate = false);

/// <summary>
/// Move a Booked appointment. Leave TechnicianId / BayId empty to keep the current ones if they are free,
/// otherwise a free qualified one is assigned. AllowLate is Admin only.
/// </summary>
public record RescheduleAppointmentRequest(DateTime NewStartTimeUtc, Guid? TechnicianId, Guid? BayId, bool AllowLate = false);

public record AddInspectionItemRequest(
    [property: Required, StringLength(200)] string Name,
    InspectionCondition Condition,
    [property: StringLength(1000)] string? Notes);

public record InspectionItemResponse(Guid Id, string Name, InspectionCondition Condition, string? Notes);

public record AppointmentResponse(
    Guid Id,
    Guid ShopId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid TechnicianId,
    Guid BayId,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    AppointmentStatus Status,
    string? CancellationReason,
    IReadOnlyList<InspectionItemResponse> InspectionItems);
