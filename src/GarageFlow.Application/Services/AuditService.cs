using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface IAuditService
{
    /// <summary>Newest first. Every filter is optional.</summary>
    Task<PagedResult<AuditLogResponse>> ListAsync(
        string? entityName, Guid? entityId, string? performedBy, PageRequest? page, CancellationToken ct = default);

    /// <summary>The full story of one appointment, oldest first.</summary>
    Task<IReadOnlyList<AuditLogResponse>> AppointmentHistoryAsync(Guid appointmentId, CancellationToken ct = default);
}

/// <summary>
/// Reads the audit trail. Writing it is automatic: the DbContext records every change to the important tables in the
/// same transaction as the change itself, so nobody can forget to log, and a change can never be saved without its record.
/// </summary>
public class AuditService(IGarageFlowDbContext db) : IAuditService
{
    public Task<PagedResult<AuditLogResponse>> ListAsync(
        string? entityName, Guid? entityId, string? performedBy, PageRequest? page, CancellationToken ct = default)
    {
        IQueryable<AuditLog> query = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var name = entityName.Trim();
            query = query.Where(a => a.EntityName == name);
        }

        if (entityId.HasValue)
        {
            var id = entityId.Value;
            query = query.Where(a => a.EntityId == id);
        }

        if (!string.IsNullOrWhiteSpace(performedBy))
        {
            var who = performedBy.Trim().ToLowerInvariant();
            query = query.Where(a => a.PerformedBy.ToLower() == who);
        }

        // Newest first; Id breaks ties so paging is stable.
        return query.OrderByDescending(a => a.TimestampUtc).ThenBy(a => a.Id)
            .ToPagedAsync(page, a => a.ToResponse(), ct);
    }

    public async Task<IReadOnlyList<AuditLogResponse>> AppointmentHistoryAsync(Guid appointmentId, CancellationToken ct = default)
    {
        if (!await db.Appointments.AnyAsync(a => a.Id == appointmentId, ct))
            throw new NotFoundException(nameof(Appointment), appointmentId);

        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Appointment) && a.EntityId == appointmentId)
            .OrderBy(a => a.TimestampUtc).ThenBy(a => a.Id)
            .ToListAsync(ct);
        return rows.Select(a => a.ToResponse()).ToList();
    }
}
