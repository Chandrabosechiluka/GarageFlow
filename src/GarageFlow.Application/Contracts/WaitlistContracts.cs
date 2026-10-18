using GarageFlow.Domain.Enums;

namespace GarageFlow.Application.Contracts;

/// <summary>
/// Ask to be told about openings. Both times are START times and must end with "Z" (UTC).
/// ShopId may be left empty while there is only one shop.
/// </summary>
public record JoinWaitlistRequest(
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    DateTime EarliestStartUtc,
    DateTime LatestStartUtc,
    Guid? ShopId = null);

public record WaitlistEntryResponse(
    Guid Id,
    Guid ShopId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    DateTime EarliestStartUtc,
    DateTime LatestStartUtc,
    WaitlistStatus Status,
    DateTime? OfferedStartUtc,
    DateTime? OfferExpiresUtc,
    Guid? AppointmentId,
    DateTime CreatedUtc);
