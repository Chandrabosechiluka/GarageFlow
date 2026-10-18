using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<ServiceTypeResponse>> ListServiceTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TechnicianResponse>> ListTechniciansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BayResponse>> ListBaysAsync(CancellationToken ct = default);

    Task<TechnicianResponse> CreateTechnicianAsync(CreateTechnicianRequest request, CancellationToken ct = default);
    Task<BayResponse> CreateBayAsync(CreateBayRequest request, CancellationToken ct = default);
    Task<ServiceTypeResponse> CreateServiceTypeAsync(CreateServiceTypeRequest request, CancellationToken ct = default);
}

/// <summary>Reference data: the things a customer or advisor picks from when booking, and the Admin's way to add to them.</summary>
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

    public async Task<TechnicianResponse> CreateTechnicianAsync(CreateTechnicianRequest request, CancellationToken ct = default)
    {
        var skills = request.Skills?.Distinct().ToList() ?? new List<Skill>();
        if (skills.Any(s => !Enum.IsDefined(s)))
            throw new DomainException("Unknown skill.");

        var shop = await ShopLookup.ResolveAsync(db, request.ShopId, ct);
        var name = request.Name.Trim();
        var lower = name.ToLower();
        if (await db.Technicians.AnyAsync(t => t.ShopId == shop.Id && t.Name.ToLower() == lower, ct))
            throw new ConflictException($"A technician named '{name}' already exists.");

        var technician = new Technician(shop.Id, name, skills);
        db.Technicians.Add(technician);
        await db.SaveChangesAsync(ct);
        return technician.ToResponse();
    }

    public async Task<BayResponse> CreateBayAsync(CreateBayRequest request, CancellationToken ct = default)
    {
        var shop = await ShopLookup.ResolveAsync(db, request.ShopId, ct);
        var name = request.Name.Trim();
        var lower = name.ToLower();
        if (await db.Bays.AnyAsync(b => b.ShopId == shop.Id && b.Name.ToLower() == lower, ct))
            throw new ConflictException($"A bay named '{name}' already exists.");

        var bay = new Bay(shop.Id, name);
        db.Bays.Add(bay);
        await db.SaveChangesAsync(ct);
        return bay.ToResponse();
    }

    public async Task<ServiceTypeResponse> CreateServiceTypeAsync(CreateServiceTypeRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.RequiredSkill))
            throw new DomainException("Unknown skill.");

        var name = request.Name.Trim();
        var lower = name.ToLower();
        if (await db.ServiceTypes.AnyAsync(s => s.Name.ToLower() == lower, ct))
            throw new ConflictException($"A service named '{name}' already exists.");

        var serviceType = new ServiceType(name, request.DurationMinutes, request.RequiredSkill);
        db.ServiceTypes.Add(serviceType);
        await db.SaveChangesAsync(ct);
        return serviceType.ToResponse();
    }
}
