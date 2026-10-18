namespace GarageFlow.Application.Common;

/// <summary>The request is valid but clashes with existing data (duplicate email, double booking). The API maps this to HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
