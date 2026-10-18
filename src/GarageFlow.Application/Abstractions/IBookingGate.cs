namespace GarageFlow.Application.Abstractions;

/// <summary>
/// Makes "check that the slot is free, then take it" one indivisible step.
/// Callers that use the same key never run their action at the same time, and every database change made inside
/// the action is committed together or rolled back together. Without this, two requests arriving at the same
/// moment can both see "free" and both book the same technician (a race condition).
/// </summary>
public interface IBookingGate
{
    Task<T> RunExclusiveAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
}
