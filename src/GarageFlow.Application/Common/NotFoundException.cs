namespace GarageFlow.Application.Common;

/// <summary>The thing the caller asked for does not exist. The API maps this to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, Guid id)
        : base($"{entityName} with id {id} was not found.")
    {
    }
}
