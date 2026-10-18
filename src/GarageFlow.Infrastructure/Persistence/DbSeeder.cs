using GarageFlow.Application.Abstractions;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Persistence;

/// <summary>
/// Inserts demo data so the API has something to work with. Safe to call on every start:
/// it does nothing if a shop already exists. The API only calls it in Development.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(GarageFlowDbContext db, CancellationToken ct = default)
    {
        if (await db.Shops.AnyAsync(ct))
            return;

        var shop = new Shop("GarageFlow Central", new TimeOnly(9, 0), new TimeOnly(18, 0));
        db.Shops.Add(shop);

        db.ServiceTypes.AddRange(
            new ServiceType("Oil Change", 45, Skill.OilChange),
            new ServiceType("Brake Inspection", 60, Skill.Brakes),
            new ServiceType("Tyre Rotation", 30, Skill.Tires),
            new ServiceType("Engine Diagnostics", 90, Skill.Engine),
            new ServiceType("General Service", 120, Skill.General));

        db.Technicians.AddRange(
            new Technician(shop.Id, "Ravi Kumar", new[] { Skill.General, Skill.OilChange, Skill.Tires }),
            new Technician(shop.Id, "Anil Reddy", new[] { Skill.General, Skill.Brakes }),
            new Technician(shop.Id, "Meena Sharma", new[] { Skill.General, Skill.Engine, Skill.Electrical }));

        db.Bays.AddRange(
            new Bay(shop.Id, "Bay 1"),
            new Bay(shop.Id, "Bay 2"),
            new Bay(shop.Id, "Bay 3"));

        // One ready-made customer so you can book from Swagger straight away.
        var customer = new Customer("Demo Customer", "demo@example.com", "9000000000");
        db.Vehicles.Add(customer.AddVehicle("Maruti", "Swift", 2020, "TS09AB1234"));
        db.Customers.Add(customer);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// DEVELOPMENT ONLY demo logins, one per role. The shared password is public in this repo, so these accounts
    /// must never exist in a real environment (the API only calls this in Development).
    /// </summary>
    public const string DevPassword = "Garage@12345";

    public static async Task SeedUsersAsync(GarageFlowDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
            return;

        db.Users.AddRange(
            new AppUser("admin@garageflow.local", "Demo Admin", hasher.Hash(DevPassword), UserRole.Admin),
            new AppUser("advisor@garageflow.local", "Demo Advisor", hasher.Hash(DevPassword), UserRole.Advisor),
            new AppUser("tech@garageflow.local", "Demo Technician", hasher.Hash(DevPassword), UserRole.Technician));

        await db.SaveChangesAsync(ct);
    }
}
