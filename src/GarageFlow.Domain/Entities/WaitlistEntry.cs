using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;

namespace GarageFlow.Domain.Entities;

/// <summary>
/// "Tell me if a slot opens up." The customer gives a window of acceptable START times.
/// When an appointment is cancelled or moved, the oldest matching entry is Offered that slot.
/// Waiting -> Offered -> Accepting -> Fulfilled, or Cancelled; a failed accept goes back to Waiting.
/// </summary>
public class WaitlistEntry : BaseEntity
{
    public const int MaxWindowDays = 30;

    /// <summary>How long a customer has to answer an offer before it is passed on.</summary>
    public static readonly TimeSpan OfferLifetime = TimeSpan.FromHours(2);

    private WaitlistEntry()
    {
    }

    public Guid ShopId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid ServiceTypeId { get; private set; }

    public DateTime EarliestStartUtc { get; private set; }
    public DateTime LatestStartUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public WaitlistStatus Status { get; private set; }
    public DateTime? OfferedStartUtc { get; private set; }
    public DateTime? OfferedEndUtc { get; private set; }
    public Guid? OfferedTechnicianId { get; private set; }
    public DateTime? OfferExpiresUtc { get; private set; }

    /// <summary>Set by the notification job once the customer has been told about the offer (so they are told only once).</summary>
    public DateTime? OfferNotifiedUtc { get; private set; }
    public Guid? AppointmentId { get; private set; }

    /// <summary>Stops two requests changing the same entry at once (for example two accepts).</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static WaitlistEntry Join(
        Guid shopId, Guid customerId, Guid vehicleId, Guid serviceTypeId,
        DateTime earliestStartUtc, DateTime latestStartUtc, DateTime nowUtc)
    {
        if (earliestStartUtc.Kind != DateTimeKind.Utc || latestStartUtc.Kind != DateTimeKind.Utc)
            throw new DomainException("Waitlist times must be in UTC.");
        if (latestStartUtc < earliestStartUtc)
            throw new DomainException("The latest start must not be before the earliest start.");
        if (latestStartUtc <= nowUtc)
            throw new DomainException("The waitlist window must end in the future.");
        if (latestStartUtc - earliestStartUtc > TimeSpan.FromDays(MaxWindowDays))
            throw new DomainException($"The waitlist window cannot be longer than {MaxWindowDays} days.");

        return new WaitlistEntry
        {
            ShopId = shopId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ServiceTypeId = serviceTypeId,
            EarliestStartUtc = earliestStartUtc,
            LatestStartUtc = latestStartUtc,
            CreatedUtc = nowUtc,
            Status = WaitlistStatus.Waiting
        };
    }

    public bool WantsStart(DateTime startUtc) => startUtc >= EarliestStartUtc && startUtc <= LatestStartUtc;

    public void Offer(DateTime startUtc, DateTime endUtc, Guid technicianId, DateTime nowUtc)
    {
        if (Status != WaitlistStatus.Waiting)
            throw new DomainException($"Only a waiting entry can be offered a slot (this one is {Status}).");
        if (!WantsStart(startUtc))
            throw new DomainException("The offered start is outside the customer's window.");
        if (endUtc <= startUtc)
            throw new DomainException("The offered slot must end after it starts.");

        OfferedStartUtc = startUtc;
        OfferedEndUtc = endUtc;
        OfferedTechnicianId = technicianId;
        OfferExpiresUtc = nowUtc + OfferLifetime;
        OfferNotifiedUtc = null;
        Status = WaitlistStatus.Offered;
    }

    public bool IsOfferExpired(DateTime nowUtc) =>
        Status == WaitlistStatus.Offered && OfferExpiresUtc is { } expires && expires <= nowUtc;

    /// <summary>The offer lapsed without an answer. The slot details stay on the entry as history.</summary>
    public void Expire(DateTime nowUtc)
    {
        if (!IsOfferExpired(nowUtc))
            throw new DomainException("Only an offer whose time is up can be expired.");

        Status = WaitlistStatus.Expired;
    }

    public void MarkOfferNotified(DateTime nowUtc)
    {
        if (Status != WaitlistStatus.Offered)
            throw new DomainException($"Only an open offer can be marked as notified (this entry is {Status}).");

        OfferNotifiedUtc = nowUtc;
    }

    /// <summary>First step of accepting: claims the offer and returns the start time to book.</summary>
    public DateTime BeginAccept(DateTime nowUtc)
    {
        if (Status != WaitlistStatus.Offered || OfferedStartUtc is null)
            throw new DomainException($"There is no open offer to accept (this entry is {Status}).");
        if (IsOfferExpired(nowUtc))
            throw new DomainException("This offer has expired.");

        Status = WaitlistStatus.Accepting;
        return OfferedStartUtc.Value;
    }

    public void Fulfil(Guid appointmentId)
    {
        if (Status != WaitlistStatus.Accepting)
            throw new DomainException($"Only an entry being accepted can be fulfilled (this one is {Status}).");

        AppointmentId = appointmentId;
        Status = WaitlistStatus.Fulfilled;
    }

    /// <summary>The slot could not be booked after all: back to waiting for the next opening.</summary>
    public void WithdrawOffer()
    {
        if (Status is not (WaitlistStatus.Offered or WaitlistStatus.Accepting))
            throw new DomainException($"There is no offer to withdraw (this entry is {Status}).");

        OfferedStartUtc = null;
        OfferedEndUtc = null;
        OfferedTechnicianId = null;
        OfferExpiresUtc = null;
        OfferNotifiedUtc = null;
        Status = WaitlistStatus.Waiting;
    }

    public void Cancel()
    {
        if (Status is WaitlistStatus.Fulfilled or WaitlistStatus.Cancelled or WaitlistStatus.Accepting or WaitlistStatus.Expired)
            throw new DomainException($"This waitlist entry cannot be cancelled (it is {Status}).");

        Status = WaitlistStatus.Cancelled;
    }
}
