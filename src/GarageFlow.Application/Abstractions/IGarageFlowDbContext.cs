using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Abstractions;

/// <summary>
/// What the Application layer needs from the database. The real implementation (GarageFlowDbContext)
/// lives in Infrastructure, so Application never depends on SQL Server.
/// </summary>
public interface IGarageFlowDbContext
{
    DbSet<Shop> Shops { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<ServiceType> ServiceTypes { get; }
    DbSet<Technician> Technicians { get; }
    DbSet<Bay> Bays { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<InspectionItem> InspectionItems { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
