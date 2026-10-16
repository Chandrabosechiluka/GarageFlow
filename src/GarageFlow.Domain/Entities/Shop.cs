using GarageFlow.Domain.Common;

namespace GarageFlow.Domain.Entities;

public class Shop : BaseEntity
{
    private Shop()
    {
    }

    public Shop(string name, TimeOnly openingTime, TimeOnly closingTime)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Shop name is required.");
        if (closingTime <= openingTime)
            throw new DomainException("Closing time must be after opening time.");

        Name = name.Trim();
        OpeningTime = openingTime;
        ClosingTime = closingTime;
    }

    public string Name { get; private set; } = string.Empty;
    public TimeOnly OpeningTime { get; private set; }
    public TimeOnly ClosingTime { get; private set; }

    /// <summary>True when the whole interval [start, end) falls inside working hours.</summary>
    public bool IsOpenFor(TimeOnly start, TimeOnly end) =>
        start < end && start >= OpeningTime && end <= ClosingTime;
}
