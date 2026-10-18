using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Entities;
using GarageFlow.Domain.Enums;
using GarageFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

/// <summary>A clock that always says the same time, so "must start in the future" is predictable.</summary>
public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow => utcNow;
}

/// <summary>
/// A small, ready-made garage: one shop, one 60-minute brake service, two technicians who can do it,
/// two bays and one customer with one vehicle. Each test gets its own fresh in-memory database.
/// </summary>
public class Scenario
{
    public static readonly DateTime Now = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime NineAm = new(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

    public GarageFlowDbContext Db { get; }
    public FixedClock Clock { get; } = new(Now);

    public Shop Shop { get; }
    public ServiceType BrakeService { get; }
    public Technician Tech1 { get; }
    public Technician Tech2 { get; }
    public Bay Bay1 { get; }
    public Bay Bay2 { get; }
    public Customer Customer { get; }
    public Vehicle Vehicle { get; }

    public Scenario()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new GarageFlowDbContext(options);

        Shop = new Shop("Test Garage", new TimeOnly(9, 0), new TimeOnly(18, 0));
        BrakeService = new ServiceType("Brake Inspection", 60, Skill.Brakes);
        Tech1 = new Technician(Shop.Id, "Tech One", new[] { Skill.Brakes });
        Tech2 = new Technician(Shop.Id, "Tech Two", new[] { Skill.Brakes });
        Bay1 = new Bay(Shop.Id, "Bay 1");
        Bay2 = new Bay(Shop.Id, "Bay 2");
        Customer = new Customer("Test Customer", "test@example.com", "9000000001");
        Vehicle = Customer.AddVehicle("Honda", "City", 2019, "TS01AA0001");

        Db.Shops.Add(Shop);
        Db.ServiceTypes.Add(BrakeService);
        Db.Technicians.AddRange(Tech1, Tech2);
        Db.Bays.AddRange(Bay1, Bay2);
        Db.Customers.Add(Customer);
        Db.SaveChanges();
    }

    public BookAppointmentRequest Request(Technician technician, Bay bay, DateTime startUtc) =>
        new(Customer.Id, Vehicle.Id, BrakeService.Id, technician.Id, bay.Id, startUtc);

    /// <summary>A booking request that leaves technician and bay empty, so the system assigns them.</summary>
    public BookAppointmentRequest RequestAuto(DateTime startUtc) =>
        new(Customer.Id, Vehicle.Id, BrakeService.Id, null, null, startUtc);
}
