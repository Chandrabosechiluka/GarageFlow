namespace GarageFlow.Application.Contracts;

public record AuditLogResponse(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Action,
    string PerformedBy,
    DateTime TimestampUtc,
    string? Details);
