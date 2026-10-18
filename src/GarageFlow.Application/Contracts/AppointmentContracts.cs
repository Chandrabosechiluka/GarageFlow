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

public record CancelAppointmentRequest([property: Required, StringLength(500)] string Reason);

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
