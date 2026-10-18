using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Common;
using GarageFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

public class CatalogAdminTests
{
    [Fact]
    public async Task CreateTechnician_StoresTheTrimmedNameAndDistinctSkills_InTheShop()
    {
        var s = new Scenario();
        var catalog = new CatalogService(s.Db);

        var created = await catalog.CreateTechnicianAsync(new CreateTechnicianRequest("  Kiran Rao  ", new[] { Skill.Brakes, Skill.Brakes, Skill.Engine }));

        Assert.Equal("Kiran Rao", created.Name);
        Assert.Equal(s.Shop.Id, created.ShopId);
        Assert.Equal(2, created.Skills.Count);
        Assert.Contains(await catalog.ListTechniciansAsync(), t => t.Id == created.Id);
    }

    [Fact]
    public async Task CreateTechnician_WithoutSkills_IsRejected()
    {
        var catalog = new CatalogService(new Scenario().Db);
        await Assert.ThrowsAsync<DomainException>(() => catalog.CreateTechnicianAsync(new CreateTechnicianRequest("Nobody", Array.Empty<Skill>())));
    }

    [Fact]
    public async Task CreateTechnician_WithAnUnknownSkillNumber_IsRejected()
    {
        var catalog = new CatalogService(new Scenario().Db);
        await Assert.ThrowsAsync<DomainException>(() => catalog.CreateTechnicianAsync(new CreateTechnicianRequest("Odd", new[] { (Skill)9999 })));
    }

    [Fact]
    public async Task CreateTechnician_WithADuplicateName_IgnoresCase_AndIsAConflict()
    {
        var s = new Scenario();
        var catalog = new CatalogService(s.Db);

        await Assert.ThrowsAsync<ConflictException>(() => catalog.CreateTechnicianAsync(new CreateTechnicianRequest("TECH ONE", new[] { Skill.Brakes })));
    }

    [Fact]
    public async Task CreateBay_Works_AndADuplicateNameIsAConflict()
    {
        var s = new Scenario();
        var catalog = new CatalogService(s.Db);

        var created = await catalog.CreateBayAsync(new CreateBayRequest("Bay 3"));
        Assert.Equal(s.Shop.Id, created.ShopId);
        await Assert.ThrowsAsync<ConflictException>(() => catalog.CreateBayAsync(new CreateBayRequest("bay 3")));
    }

    [Fact]
    public async Task CreateBay_ForAShopThatDoesNotExist_IsNotFound()
    {
        var catalog = new CatalogService(new Scenario().Db);
        await Assert.ThrowsAsync<NotFoundException>(() => catalog.CreateBayAsync(new CreateBayRequest("Bay 9", Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateServiceType_Works_AndRejectsDuplicatesAndBadDurations()
    {
        var s = new Scenario();
        var catalog = new CatalogService(s.Db);

        var created = await catalog.CreateServiceTypeAsync(new CreateServiceTypeRequest("Wheel Alignment", 60, Skill.Tires));
        Assert.Equal(60, created.DurationMinutes);

        await Assert.ThrowsAsync<ConflictException>(() => catalog.CreateServiceTypeAsync(new CreateServiceTypeRequest("brake inspection", 30, Skill.Brakes)));
        await Assert.ThrowsAsync<DomainException>(() => catalog.CreateServiceTypeAsync(new CreateServiceTypeRequest("Zero", 0, Skill.General)));
    }

    [Fact]
    public async Task ACreatedTechnicianAndBay_CanBeBooked()
    {
        var s = new Scenario();
        var catalog = new CatalogService(s.Db);
        var tech = await catalog.CreateTechnicianAsync(new CreateTechnicianRequest("New Tech", new[] { Skill.Brakes }));
        var bay = await catalog.CreateBayAsync(new CreateBayRequest("Bay 3"));

        var booked = await s.Appointments().BookAsync(new BookAppointmentRequest(
            s.Customer.Id, s.Vehicle.Id, s.BrakeService.Id, tech.Id, bay.Id, Scenario.NineAm));

        Assert.Equal(tech.Id, booked.TechnicianId);
        Assert.Equal(bay.Id, booked.BayId);
    }
}
