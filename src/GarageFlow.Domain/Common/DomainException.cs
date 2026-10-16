namespace GarageFlow.Domain.Common;

/// <summary>
/// Thrown when a business rule is violated. The API layer will later map this to HTTP 400/409.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
