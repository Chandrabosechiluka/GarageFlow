using GarageFlow.Domain.Common;

namespace GarageFlow.Domain.Entities;

/// <summary>Append-only record of who changed what and when.</summary>
public class AuditLog : BaseEntity
{
    private AuditLog()
    {
    }

    public static AuditLog Create(string entityName, Guid entityId, string action, string performedBy, DateTime nowUtc, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            throw new DomainException("Entity name is required.");
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("Action is required.");
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new DomainException("PerformedBy is required.");

        return new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            PerformedBy = performedBy,
            TimestampUtc = nowUtc,
            Details = details
        };
    }

    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string PerformedBy { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; }
    public string? Details { get; private set; }
}
