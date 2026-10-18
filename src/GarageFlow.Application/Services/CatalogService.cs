using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<ServiceTypeResponse>> ListServiceTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TechnicianResponse>> ListTechniciansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BayResponse>> ListBaysAsync(CancellationToken ct = default);
}

/// <summary>Read-only reference data: the things a customer or advisor picks from when booking.</summary>
public class CatalogService(IGarageFlowDbContext db) : ICatalogService
{
    public async Task<IReadOnlyList<ServiceTypeResponse>> ListServiceTypesAsync(CancellationToken ct = default)
    {
        var items = await db.ServiceTypes.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        return items.Select(s => s.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<TechnicianResponse>> ListTechniciansAsync(CancellationToken ct = default)
    {
        var items = await db.Technicians.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        return items.Select(t => t.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<BayResponse>> ListBaysAsync(CancellationToken ct = default)
    {
        var items = await db.Bays.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        return items.Select(b => b.ToResponse()).ToList();
    }
}
