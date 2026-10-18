namespace GarageFlow.Application.Contracts;

/// <summary>
/// One bookable start time. Times come in UTC (what the API stores) and in the shop's local time (what a person reads).
/// The "suggested" ids are what the system will assign if the booking request leaves technician and bay empty.
/// </summary>
public record SlotResponse(
    DateTime StartUtc,
    DateTime EndUtc,
    DateTimeOffset StartLocal,
    DateTimeOffset EndLocal,
    Guid SuggestedTechnicianId,
    Guid SuggestedBayId,
    IReadOnlyList<Guid> AvailableTechnicianIds,
    IReadOnlyList<Guid> AvailableBayIds);

public record AvailabilityResponse(
    DateOnly Date,
    Guid ShopId,
    string TimeZoneId,
    Guid ServiceTypeId,
    int DurationMinutes,
    IReadOnlyList<SlotResponse> Slots);
