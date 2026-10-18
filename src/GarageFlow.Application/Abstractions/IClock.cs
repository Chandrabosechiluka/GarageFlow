namespace GarageFlow.Application.Abstractions;

/// <summary>Source of "now". Services ask this instead of calling DateTime.UtcNow so tests can fix the time.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
