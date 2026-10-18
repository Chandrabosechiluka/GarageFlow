namespace GarageFlow.Domain.Enums;

public enum WaitlistStatus
{
    /// <summary>Waiting for a slot to open up.</summary>
    Waiting = 1,

    /// <summary>A freed slot was offered; the customer has not answered yet.</summary>
    Offered = 2,

    /// <summary>Someone is turning the offer into a booking right now (stops two accepts at once).</summary>
    Accepting = 3,

    /// <summary>The offer became a real appointment.</summary>
    Fulfilled = 4,

    /// <summary>The customer left the waitlist.</summary>
    Cancelled = 5
}
