using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Tests;

public class CustomerServiceTests
{
    [Fact]
    public async Task Create_NewCustomer_IsSavedWithLowercaseEmail()
    {
        var s = new Scenario();
        var service = new CustomerService(s.Db);

        var created = await service.CreateAsync(new CreateCustomerRequest("Asha Rao", "Asha.Rao@Example.com", "9111111111"));

        Assert.Equal("asha.rao@example.com", created.Email);
        Assert.Equal(2, await s.Db.Customers.CountAsync()); // the scenario's customer plus this one
    }

    [Fact]
    public async Task Create_DuplicateEmail_IgnoringCase_IsAConflict()
    {
        var s = new Scenario();
        var service = new CustomerService(s.Db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(new CreateCustomerRequest("Someone Else", "TEST@example.com", "9222222222")));
    }

    [Fact]
    public async Task AddVehicle_DuplicateRegistration_IsRejectedByTheDomain()
    {
        var s = new Scenario();
        var service = new CustomerService(s.Db);

        // "ts 01-aa 0001" normalises to the scenario vehicle's registration TS01AA0001.
        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddVehicleAsync(s.Customer.Id, new AddVehicleRequest("Honda", "Amaze", 2021, "ts 01-aa 0001")));
    }

    [Fact]
    public async Task AddVehicle_ForUnknownCustomer_ThrowsNotFound()
    {
        var s = new Scenario();
        var service = new CustomerService(s.Db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddVehicleAsync(Guid.NewGuid(), new AddVehicleRequest("Kia", "Seltos", 2023, "TS03CC0003")));
    }

    [Fact]
    public async Task AddVehicle_NewRegistration_IsReturnedWithCustomer()
    {
        var s = new Scenario();
        var service = new CustomerService(s.Db);

        await service.AddVehicleAsync(s.Customer.Id, new AddVehicleRequest("Kia", "Seltos", 2023, "TS03CC0003"));
        var customer = await service.GetAsync(s.Customer.Id);

        Assert.Equal(2, customer.Vehicles.Count);
    }
}
