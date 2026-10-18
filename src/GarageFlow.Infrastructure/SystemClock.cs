using GarageFlow.Application.Abstractions;

namespace GarageFlow.Infrastructure;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
